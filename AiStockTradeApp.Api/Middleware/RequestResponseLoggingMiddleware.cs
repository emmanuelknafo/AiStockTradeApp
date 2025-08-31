using System.Diagnostics;
using System.Text;

namespace AiStockTradeApp.Api.Middleware;

/// <summary>
/// Middleware for comprehensive request/response logging with performance metrics.
/// Logs HTTP requests, responses, timing, and error details for API monitoring and debugging.
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
    private readonly RequestResponseLoggingOptions _options;

    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestResponseLoggingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _options = configuration.GetSection("RequestResponseLogging").Get<RequestResponseLoggingOptions>() 
                   ?? new RequestResponseLoggingOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        var stopwatch = Stopwatch.StartNew();
        
        // Log incoming request
        await LogRequestAsync(context, correlationId);
        
        // Capture response for logging
        var originalResponseBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Log exception details
            _logger.LogError(ex, 
                "Unhandled exception in request {Method} {Path}. " +
                "CorrelationId: {CorrelationId}, Duration: {Duration}ms",
                context.Request.Method,
                context.Request.Path,
                correlationId,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            
            // Log response
            await LogResponseAsync(context, correlationId, stopwatch.ElapsedMilliseconds, responseBodyStream);
            
            // Copy response back to original stream
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalResponseBodyStream);
        }
    }

    private async Task LogRequestAsync(HttpContext context, string correlationId)
    {
        var request = context.Request;
        var requestInfo = new
        {
            Method = request.Method,
            Path = request.Path.Value,
            QueryString = request.QueryString.Value,
            Headers = GetSafeHeaders(request.Headers),
            ContentType = request.ContentType,
            ContentLength = request.ContentLength,
            UserAgent = request.Headers.UserAgent.FirstOrDefault(),
            RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = correlationId
        };

        // Log request body for non-GET requests if enabled and size is reasonable
        string? requestBody = null;
        if (_options.LogRequestBody && 
            !string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase) &&
            request.ContentLength.HasValue && 
            request.ContentLength.Value > 0 && 
            request.ContentLength.Value <= _options.MaxBodySizeToLog)
        {
            requestBody = await ReadRequestBodyAsync(request);
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestMethod"] = request.Method,
            ["RequestPath"] = request.Path.Value ?? string.Empty
        });

        if (!string.IsNullOrEmpty(requestBody))
        {
            _logger.LogInformation(
                "HTTP Request {Method} {Path} - CorrelationId: {CorrelationId}, " +
                "ContentType: {ContentType}, ContentLength: {ContentLength}, " +
                "UserAgent: {UserAgent}, RemoteIP: {RemoteIP}, Body: {RequestBody}",
                request.Method, request.Path, correlationId,
                request.ContentType, request.ContentLength,
                requestInfo.UserAgent, requestInfo.RemoteIpAddress, requestBody);
        }
        else
        {
            _logger.LogInformation(
                "HTTP Request {Method} {Path} - CorrelationId: {CorrelationId}, " +
                "ContentType: {ContentType}, ContentLength: {ContentLength}, " +
                "UserAgent: {UserAgent}, RemoteIP: {RemoteIP}",
                request.Method, request.Path, correlationId,
                request.ContentType, request.ContentLength,
                requestInfo.UserAgent, requestInfo.RemoteIpAddress);
        }
    }

    private async Task LogResponseAsync(HttpContext context, string correlationId, long durationMs, MemoryStream responseBodyStream)
    {
        var response = context.Response;
        
        // Log response body if enabled and size is reasonable
        string? responseBody = null;
        if (_options.LogResponseBody && 
            responseBodyStream.Length > 0 && 
            responseBodyStream.Length <= _options.MaxBodySizeToLog &&
            IsTextContentType(response.ContentType))
        {
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();
            responseBodyStream.Seek(0, SeekOrigin.Begin);
        }

        var logLevel = response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information;
        
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["ResponseStatusCode"] = response.StatusCode,
            ["Duration"] = durationMs
        });

        if (!string.IsNullOrEmpty(responseBody))
        {
            _logger.Log(logLevel,
                "HTTP Response {StatusCode} - CorrelationId: {CorrelationId}, " +
                "Duration: {Duration}ms, ContentType: {ContentType}, " +
                "ContentLength: {ContentLength}, Body: {ResponseBody}",
                response.StatusCode, correlationId, durationMs,
                response.ContentType, responseBodyStream.Length, responseBody);
        }
        else
        {
            _logger.Log(logLevel,
                "HTTP Response {StatusCode} - CorrelationId: {CorrelationId}, " +
                "Duration: {Duration}ms, ContentType: {ContentType}, ContentLength: {ContentLength}",
                response.StatusCode, correlationId, durationMs,
                response.ContentType, responseBodyStream.Length);
        }

        // Log performance metrics
        if (durationMs > _options.SlowRequestThresholdMs)
        {
            _logger.LogWarning(
                "Slow request detected: {Method} {Path} took {Duration}ms - CorrelationId: {CorrelationId}",
                context.Request.Method, context.Request.Path, durationMs, correlationId);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        const string correlationIdHeader = "X-Correlation-ID";
        
        if (context.Request.Headers.TryGetValue(correlationIdHeader, out var existingId) && 
            !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId.ToString();
        }

        var correlationId = Guid.NewGuid().ToString("N")[..8]; // Short correlation ID
        context.Response.Headers[correlationIdHeader] = correlationId;
        return correlationId;
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        var body = request.Body;
        body.Seek(0, SeekOrigin.Begin);
        
        using var reader = new StreamReader(body, Encoding.UTF8, leaveOpen: true);
        var content = await reader.ReadToEndAsync();
        body.Seek(0, SeekOrigin.Begin);
        
        return content;
    }

    private static Dictionary<string, string?[]> GetSafeHeaders(IHeaderDictionary headers)
    {
        var safeHeaders = new Dictionary<string, string?[]>();
        var sensitiveHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization", "Cookie", "X-API-Key", "X-Auth-Token"
        };

        foreach (var header in headers)
        {
            if (sensitiveHeaders.Contains(header.Key))
            {
                safeHeaders[header.Key] = new[] { "[REDACTED]" };
            }
            else
            {
                safeHeaders[header.Key] = header.Value.ToArray();
            }
        }

        return safeHeaders;
    }

    private static bool IsTextContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        
        return contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Configuration options for request/response logging middleware.
/// </summary>
public class RequestResponseLoggingOptions
{
    /// <summary>
    /// Whether to log request bodies (default: false for security).
    /// </summary>
    public bool LogRequestBody { get; set; } = false;

    /// <summary>
    /// Whether to log response bodies (default: false for performance).
    /// </summary>
    public bool LogResponseBody { get; set; } = false;

    /// <summary>
    /// Maximum body size to log in bytes (default: 4KB).
    /// </summary>
    public int MaxBodySizeToLog { get; set; } = 4096;

    /// <summary>
    /// Threshold in milliseconds to consider a request as slow (default: 2000ms).
    /// </summary>
    public int SlowRequestThresholdMs { get; set; } = 2000;
}
