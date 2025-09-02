using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using System.Diagnostics;
using System.Text.Json;

namespace AiStockTradeApp.McpServer.Middleware;

/// <summary>
/// Middleware to track MCP tool usage and consumer interactions for Application Insights telemetry.
/// </summary>
public class McpTelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<McpTelemetryMiddleware> _logger;
    private readonly TelemetryClient _telemetryClient;

    public McpTelemetryMiddleware(RequestDelegate next, ILogger<McpTelemetryMiddleware> logger, TelemetryClient telemetryClient)
    {
        _next = next;
        _logger = logger;
        _telemetryClient = telemetryClient;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        
        // Capture request details
        var userAgent = context.Request.Headers.UserAgent.FirstOrDefault() ?? "Unknown";
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var consumerIdentifier = ExtractConsumerIdentifier(context, userAgent);
        
        _logger.LogInformation("MCP Request started - RequestId: {RequestId}, Consumer: {Consumer}, IP: {ClientIp}, UserAgent: {UserAgent}",
            requestId, consumerIdentifier, clientIp, userAgent);

        // Track the request in Application Insights
        var requestTelemetry = new RequestTelemetry
        {
            Name = "MCP Request",
            Id = requestId,
            Timestamp = DateTimeOffset.UtcNow,
            Url = new Uri(context.Request.GetDisplayUrl()),
            Properties = 
            {
                ["Consumer"] = consumerIdentifier,
                ["ClientIP"] = clientIp,
                ["UserAgent"] = userAgent,
                ["Protocol"] = "MCP",
                ["Method"] = context.Request.Method
            }
        };

        // Capture request body for tool tracking
        string? requestBody = null;
        if (context.Request.ContentLength > 0 && context.Request.ContentType?.Contains("application/json") == true)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
            
            // Extract tool information from MCP request
            await TrackMcpToolUsage(requestBody, consumerIdentifier, requestId);
        }

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await _next(context);
            
            stopwatch.Stop();
            requestTelemetry.Duration = stopwatch.Elapsed;
            requestTelemetry.Success = context.Response.StatusCode < 400;
            requestTelemetry.ResponseCode = context.Response.StatusCode.ToString();

            // Log response details
            await LogResponseDetails(context, responseBodyStream, requestId, consumerIdentifier, stopwatch.Elapsed);
            
            _telemetryClient.TrackRequest(requestTelemetry);
            
            _logger.LogInformation("MCP Request completed - RequestId: {RequestId}, Consumer: {Consumer}, StatusCode: {StatusCode}, Duration: {Duration}ms",
                requestId, consumerIdentifier, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            requestTelemetry.Duration = stopwatch.Elapsed;
            requestTelemetry.Success = false;
            requestTelemetry.ResponseCode = "500";
            
            _logger.LogError(ex, "MCP Request failed - RequestId: {RequestId}, Consumer: {Consumer}, Duration: {Duration}ms",
                requestId, consumerIdentifier, stopwatch.ElapsedMilliseconds);
            
            _telemetryClient.TrackRequest(requestTelemetry);
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["RequestId"] = requestId,
                ["Consumer"] = consumerIdentifier,
                ["ClientIP"] = clientIp
            });
            
            throw;
        }
        finally
        {
            await responseBodyStream.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
    }

    private string ExtractConsumerIdentifier(HttpContext context, string userAgent)
    {
        // Try to identify the consumer from various sources
        var customHeader = context.Request.Headers["X-MCP-Consumer"].FirstOrDefault();
        if (!string.IsNullOrEmpty(customHeader))
            return customHeader;

        // Extract from User-Agent patterns
        if (userAgent.Contains("Claude", StringComparison.OrdinalIgnoreCase))
            return "Claude Desktop";
        if (userAgent.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase))
            return "ChatGPT";
        if (userAgent.Contains("Cursor", StringComparison.OrdinalIgnoreCase))
            return "Cursor IDE";
        if (userAgent.Contains("VSCode", StringComparison.OrdinalIgnoreCase))
            return "VS Code";

        // Extract from Referer or Origin
        var referer = context.Request.Headers.Referer.FirstOrDefault();
        if (!string.IsNullOrEmpty(referer))
        {
            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            {
                return $"Web Client ({uri.Host})";
            }
        }

        return $"Unknown ({userAgent.Split(' ').FirstOrDefault() ?? "Unknown"})";
    }

    private Task TrackMcpToolUsage(string requestBody, string consumer, string requestId)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(requestBody);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("method", out var methodElement))
            {
                var method = methodElement.GetString();
                
                if (method == "tools/call" && root.TryGetProperty("params", out var paramsElement))
                {
                    if (paramsElement.TryGetProperty("name", out var nameElement))
                    {
                        var toolName = nameElement.GetString() ?? "Unknown";
                        var arguments = new Dictionary<string, object>();
                        
                        if (paramsElement.TryGetProperty("arguments", out var argsElement))
                        {
                            foreach (var prop in argsElement.EnumerateObject())
                            {
                                arguments[prop.Name] = prop.Value.ToString() ?? "";
                            }
                        }

                        // Track tool usage event
                        _telemetryClient.TrackEvent("MCP Tool Called", new Dictionary<string, string>
                        {
                            ["RequestId"] = requestId,
                            ["Consumer"] = consumer,
                            ["ToolName"] = toolName,
                            ["Arguments"] = JsonSerializer.Serialize(arguments)
                        });

                        // Track custom metric for tool usage
                        _telemetryClient.TrackMetric($"MCP.Tool.{toolName}.Usage", 1, new Dictionary<string, string>
                        {
                            ["Consumer"] = consumer
                        });

                        _logger.LogInformation("MCP Tool called - RequestId: {RequestId}, Consumer: {Consumer}, Tool: {ToolName}, Arguments: {Arguments}",
                            requestId, consumer, toolName, JsonSerializer.Serialize(arguments));
                    }
                }
                else if (method == "tools/list")
                {
                    _telemetryClient.TrackEvent("MCP Tools Listed", new Dictionary<string, string>
                    {
                        ["RequestId"] = requestId,
                        ["Consumer"] = consumer
                    });

                    _logger.LogInformation("MCP Tools list requested - RequestId: {RequestId}, Consumer: {Consumer}",
                        requestId, consumer);
                }
                else
                {
                    _telemetryClient.TrackEvent("MCP Method Called", new Dictionary<string, string>
                    {
                        ["RequestId"] = requestId,
                        ["Consumer"] = consumer,
                        ["Method"] = method ?? "Unknown"
                    });

                    _logger.LogInformation("MCP Method called - RequestId: {RequestId}, Consumer: {Consumer}, Method: {Method}",
                        requestId, consumer, method);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse MCP request for telemetry tracking - RequestId: {RequestId}", requestId);
        }
        
        return Task.CompletedTask;
    }

    private async Task LogResponseDetails(HttpContext context, MemoryStream responseBodyStream, string requestId, string consumer, TimeSpan duration)
    {
        try
        {
            responseBodyStream.Position = 0;
            using var reader = new StreamReader(responseBodyStream, leaveOpen: true);
            var responseBody = await reader.ReadToEndAsync();
            responseBodyStream.Position = 0;

            if (!string.IsNullOrEmpty(responseBody) && context.Response.ContentType?.Contains("application/json") == true)
            {
                // Try to extract result information from MCP response
                try
                {
                    using var jsonDoc = JsonDocument.Parse(responseBody);
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("result", out var resultElement))
                    {
                        var resultSize = responseBody.Length;
                        var hasError = root.TryGetProperty("error", out _);

                        _telemetryClient.TrackMetric("MCP.Response.Size", resultSize, new Dictionary<string, string>
                        {
                            ["RequestId"] = requestId,
                            ["Consumer"] = consumer,
                            ["HasError"] = hasError.ToString()
                        });

                        _logger.LogDebug("MCP Response details - RequestId: {RequestId}, Consumer: {Consumer}, Size: {Size} bytes, HasError: {HasError}",
                            requestId, consumer, resultSize, hasError);
                    }
                }
                catch (JsonException)
                {
                    // Not JSON, skip parsing
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log response details - RequestId: {RequestId}", requestId);
        }
    }
}
