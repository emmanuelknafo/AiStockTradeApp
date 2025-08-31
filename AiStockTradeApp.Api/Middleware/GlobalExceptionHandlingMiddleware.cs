using System.Net;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using AiStockTradeApp.Services;

namespace AiStockTradeApp.Api.Middleware;

/// <summary>
/// Global exception handling middleware for the API.
/// Provides structured error responses and comprehensive logging for all unhandled exceptions.
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly TelemetryClient? _telemetryClient;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment,
        TelemetryClient? telemetryClient = null)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _telemetryClient = telemetryClient;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = GetCorrelationId(context);
        var requestPath = context.Request.Path.Value ?? "unknown";
        var requestMethod = context.Request.Method;
        var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();

        // Determine status code and error type based on exception
        var (statusCode, errorType, userMessage) = GetErrorDetails(exception);

        // Log exception with structured data
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["ExceptionType"] = exception.GetType().Name,
            ["RequestPath"] = requestPath,
            ["RequestMethod"] = requestMethod,
            ["StatusCode"] = (int)statusCode,
            ["UserAgent"] = userAgent ?? "unknown",
            ["RemoteIP"] = remoteIp ?? "unknown"
        });

        _logger.LogError(exception,
            "Unhandled exception occurred. " +
            "CorrelationId: {CorrelationId}, " +
            "Type: {ExceptionType}, " +
            "Path: {RequestMethod} {RequestPath}, " +
            "StatusCode: {StatusCode}, " +
            "UserAgent: {UserAgent}, " +
            "RemoteIP: {RemoteIP}, " +
            "Message: {ExceptionMessage}",
            correlationId,
            exception.GetType().Name,
            requestMethod,
            requestPath,
            (int)statusCode,
            userAgent,
            remoteIp,
            exception.Message);

        // Track exception in Application Insights
        if (_telemetryClient != null)
        {
            var telemetryException = new Microsoft.ApplicationInsights.DataContracts.ExceptionTelemetry(exception)
            {
                SeverityLevel = GetTelemetrySeverity(statusCode)
            };
            
            telemetryException.Properties.Add("CorrelationId", correlationId);
            telemetryException.Properties.Add("RequestPath", requestPath);
            telemetryException.Properties.Add("RequestMethod", requestMethod);
            telemetryException.Properties.Add("UserAgent", userAgent ?? "unknown");
            telemetryException.Properties.Add("RemoteIP", remoteIp ?? "unknown");
            
            _telemetryClient.TrackException(telemetryException);
        }

        // Create error response
        var errorResponse = new ApiErrorResponse
        {
            CorrelationId = correlationId,
            Type = errorType,
            Title = GetErrorTitle(statusCode),
            Status = (int)statusCode,
            Detail = userMessage,
            Timestamp = DateTime.UtcNow,
            Path = requestPath
        };

        // Include stack trace in development environment only
        if (_environment.IsDevelopment())
        {
            errorResponse.DeveloperMessage = exception.ToString();
        }

        // Set response details
        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        // Add correlation ID to response headers
        if (!context.Response.Headers.ContainsKey("X-Correlation-ID"))
        {
            context.Response.Headers["X-Correlation-ID"] = correlationId;
        }

        // Serialize and write response
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, jsonOptions);
        await context.Response.WriteAsync(jsonResponse);
    }

    private static (HttpStatusCode statusCode, string errorType, string userMessage) GetErrorDetails(Exception exception)
    {
        return exception switch
        {
            ArgumentException or ArgumentNullException => 
                (HttpStatusCode.BadRequest, "validation_error", "Invalid request parameters."),
            
            UnauthorizedAccessException => 
                (HttpStatusCode.Unauthorized, "authentication_error", "Authentication required."),
            
            KeyNotFoundException => 
                (HttpStatusCode.NotFound, "resource_not_found", "The requested resource was not found."),
            
            InvalidOperationException => 
                (HttpStatusCode.Conflict, "operation_error", "The requested operation cannot be completed."),
            
            NotSupportedException => 
                (HttpStatusCode.NotImplemented, "not_supported", "The requested operation is not supported."),
            
            TimeoutException => 
                (HttpStatusCode.RequestTimeout, "timeout_error", "The request timed out. Please try again."),
            
            HttpRequestException httpEx when httpEx.Message.Contains("timeout") => 
                (HttpStatusCode.GatewayTimeout, "external_service_timeout", "External service timeout. Please try again."),
            
            HttpRequestException => 
                (HttpStatusCode.BadGateway, "external_service_error", "External service unavailable. Please try again later."),
            
            TaskCanceledException => 
                (HttpStatusCode.RequestTimeout, "request_cancelled", "The request was cancelled or timed out."),
            
            _ => (HttpStatusCode.InternalServerError, "internal_error", "An internal server error occurred.")
        };
    }

    private static string GetErrorTitle(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "Bad Request",
            HttpStatusCode.Unauthorized => "Unauthorized",
            HttpStatusCode.Forbidden => "Forbidden",
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.Conflict => "Conflict",
            HttpStatusCode.RequestTimeout => "Request Timeout",
            HttpStatusCode.InternalServerError => "Internal Server Error",
            HttpStatusCode.NotImplemented => "Not Implemented",
            HttpStatusCode.BadGateway => "Bad Gateway",
            HttpStatusCode.ServiceUnavailable => "Service Unavailable",
            HttpStatusCode.GatewayTimeout => "Gateway Timeout",
            _ => "Error"
        };
    }

    private static Microsoft.ApplicationInsights.DataContracts.SeverityLevel GetTelemetrySeverity(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.InternalServerError => Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error,
            HttpStatusCode.BadGateway or 
            HttpStatusCode.ServiceUnavailable or 
            HttpStatusCode.GatewayTimeout => Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Critical,
            _ => Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning
        };
    }

    private static string GetCorrelationId(HttpContext context)
    {
        const string correlationIdHeader = "X-Correlation-ID";
        
        if (context.Request.Headers.TryGetValue(correlationIdHeader, out var existingId) && 
            !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId.ToString();
        }

        if (context.Response.Headers.TryGetValue(correlationIdHeader, out var responseId) && 
            !string.IsNullOrWhiteSpace(responseId))
        {
            return responseId.ToString();
        }

        return Guid.NewGuid().ToString("N")[..8];
    }
}

/// <summary>
/// Standardized API error response format following RFC 7807 (Problem Details for HTTP APIs).
/// </summary>
public class ApiErrorResponse
{
    /// <summary>
    /// Unique identifier for this specific error occurrence.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// A URI reference that identifies the problem type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// A short, human-readable summary of the problem type.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP status code.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// A human-readable explanation specific to this occurrence of the problem.
    /// </summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// The request path that caused the error.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Detailed error information for development environments only.
    /// </summary>
    public string? DeveloperMessage { get; set; }
}
