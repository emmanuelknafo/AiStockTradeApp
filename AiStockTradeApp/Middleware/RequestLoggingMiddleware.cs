using System.Diagnostics;
using System.Text;

namespace AiStockTradeApp.Middleware
{
    /// <summary>
    /// Middleware to log HTTP requests and responses for monitoring and debugging
    /// </summary>
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var correlationId = context.TraceIdentifier;

            // Log request start
            _logger.LogInformation(
                "Request starting: {Method} {Path} {QueryString} - Correlation: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString,
                correlationId
            );

            // Log request headers for debugging (exclude sensitive headers)
            LogRequestHeaders(context, correlationId);

            var originalBodyStream = context.Response.Body;

            try
            {
                await _next(context);
                
                stopwatch.Stop();

                // Log successful response
                _logger.LogInformation(
                    "Request completed: {Method} {Path} - Status: {StatusCode} - Duration: {Duration}ms - Correlation: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    correlationId
                );
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Log error response
                _logger.LogError(ex,
                    "Request failed: {Method} {Path} - Duration: {Duration}ms - Correlation: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds,
                    correlationId
                );

                throw; // Re-throw to let the exception handler middleware handle it
            }
        }

        private void LogRequestHeaders(HttpContext context, string correlationId)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var headers = new StringBuilder();
                foreach (var header in context.Request.Headers)
                {
                    // Skip sensitive headers
                    if (IsSensitiveHeader(header.Key))
                        continue;

                    headers.AppendLine($"  {header.Key}: {string.Join(", ", header.Value.ToArray())}");
                }

                if (headers.Length > 0)
                {
                    _logger.LogDebug(
                        "Request headers - Correlation: {CorrelationId}\n{Headers}",
                        correlationId,
                        headers.ToString()
                    );
                }
            }
        }

        private static bool IsSensitiveHeader(string headerName)
        {
            var sensitiveHeaders = new[] { "authorization", "cookie", "x-api-key", "x-auth-token" };
            return sensitiveHeaders.Contains(headerName.ToLowerInvariant());
        }
    }

    /// <summary>
    /// Extension method to register the request logging middleware
    /// </summary>
    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}
