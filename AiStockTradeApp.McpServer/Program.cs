using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Extensibility;
using ModelContextProtocol.AspNetCore;
using AiStockTradeApp.McpServer.Middleware;
using System.Collections;
using System.Diagnostics;

// Check if HTTP transport should be used via command line arguments or environment variable
static bool UseStreamableHttp(IDictionary env, string[] args)
{
    // Auto-detect Azure Container Apps or Azure App Service environment
    if (env.Contains("WEBSITE_SITE_NAME") || 
        env.Contains("CONTAINER_APP_NAME") ||
        env.Contains("WEBSITES_PORT") ||
        env.Contains("PORT"))
    {
        return true;
    }

    var useHttp = env.Contains("UseHttp") &&
                  bool.TryParse(env["UseHttp"]?.ToString()?.ToLowerInvariant(), out var result) && result;
    if (args.Length == 0)
    {
        return useHttp;
    }

    useHttp = args.Contains("--http", StringComparer.InvariantCultureIgnoreCase);
    return useHttp;
}

var useStreamableHttp = UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

// Create builder based on transport type
IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

// Add configuration from appsettings.json and environment variables
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();

// Configure Application Insights
var instrumentationKey = builder.Configuration["ApplicationInsights:InstrumentationKey"] ?? 
                        Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_INSTRUMENTATION_KEY");
var connectionString = builder.Configuration["ApplicationInsights:ConnectionString"] ?? 
                      Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

if (!string.IsNullOrEmpty(instrumentationKey) || !string.IsNullOrEmpty(connectionString))
{
    if (useStreamableHttp)
    {
        // For web applications
        builder.Services.AddApplicationInsightsTelemetry(options =>
        {
            if (!string.IsNullOrEmpty(connectionString))
                options.ConnectionString = connectionString;
            
            options.EnableAdaptiveSampling = true;
            options.EnableQuickPulseMetricStream = true;
            options.EnablePerformanceCounterCollectionModule = true;
            options.EnableEventCounterCollectionModule = true;
            options.EnableDependencyTrackingTelemetryModule = true;
        });
    }
    else
    {
        // For console applications
        builder.Services.AddApplicationInsightsTelemetryWorkerService(options =>
        {
            if (!string.IsNullOrEmpty(connectionString))
                options.ConnectionString = connectionString;
        });
    }
    
    // Add telemetry initializer for additional context
    builder.Services.AddSingleton<ITelemetryInitializer, McpTelemetryInitializer>();
}
else
{
    // Add a no-op telemetry client for development scenarios without Application Insights
    builder.Services.AddSingleton<TelemetryClient>(provider => 
    {
        var configuration = new TelemetryConfiguration();
        configuration.DisableTelemetry = true;
        return new TelemetryClient(configuration);
    });
}

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages in STDIO mode).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.AddApplicationInsights();

// Configure structured logging
builder.Logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>("", LogLevel.Information);
builder.Logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>("System", LogLevel.Warning);

// Add Activity Source for distributed tracing
builder.Services.AddSingleton<ActivitySource>(serviceProvider => 
    new ActivitySource("AiStockTradeApp.McpServer"));

// Add health checks for HTTP mode
if (useStreamableHttp)
{
    builder.Services.AddHealthChecks();
}

// Register HTTP client for API calls
builder.Services.AddHttpClient<StockTradingTools>();

// Add the MCP services with conditional transport selection
var mcpServerBuilder = builder.Services
    .AddMcpServer()
    .WithTools<StockTradingTools>()
    .WithTools<RandomNumberTools>(); // Keep the sample tool for reference

// Select transport based on the mode
if (useStreamableHttp)
{
    mcpServerBuilder.WithHttpTransport(o => o.Stateless = true);
}
else
{
    mcpServerBuilder.WithStdioServerTransport();
}

// If running as an HTTP host, allow the hosting environment to control the listen port
if (useStreamableHttp && builder is WebApplicationBuilder webBuilder)
{
    // Azure App Service and many container platforms expose the target port in PORT or WEBSITES_PORT
    var portEnv = Environment.GetEnvironmentVariable("PORT")
                  ?? Environment.GetEnvironmentVariable("WEBSITES_PORT")
                  ?? Environment.GetEnvironmentVariable("ASPNETCORE_PORT");

    if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out var port))
    {
        // Configure Kestrel/host to bind to the platform-provided port
        var urls = $"http://*:{port}";
        webBuilder.WebHost.UseUrls(urls);
        webBuilder.Configuration["ASPNETCORE_URLS"] = urls;
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", urls);
    }
    else
    {
        // Fall back to any ASPNETCORE_URLS set by the environment (or keep defaults)
        var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        if (!string.IsNullOrEmpty(urls))
        {
            webBuilder.WebHost.UseUrls(urls);
            webBuilder.Configuration["ASPNETCORE_URLS"] = urls;
        }
    }
}

// Build and configure the application
IHost app;
if (useStreamableHttp)
{
    var webApp = (builder as WebApplicationBuilder)!.Build();
    
    // Add telemetry middleware for HTTP mode
    webApp.UseMiddleware<McpTelemetryMiddleware>();
    
    // Comment out HTTPS redirection for testing
    // webApp.UseHttpsRedirection();
    
    // Map health endpoint
    webApp.MapHealthChecks("/health");
    
    // Map MCP endpoint
    webApp.MapMcp("/mcp");

    app = webApp;
}
else
{
    var consoleApp = (builder as HostApplicationBuilder)!.Build();
    app = consoleApp;
}

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var configuration = app.Services.GetRequiredService<IConfiguration>();
var telemetryClient = app.Services.GetRequiredService<TelemetryClient>();

var apiBaseUrl = configuration["STOCK_API_BASE_URL"] ?? 
                Environment.GetEnvironmentVariable("STOCK_API_BASE_URL") ?? 
                "http://localhost:5000"; // Default to localhost for development

var appInsightsKey = configuration["ApplicationInsights:InstrumentationKey"] ?? 
                    Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_INSTRUMENTATION_KEY");
var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"] ?? 
                                 Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

logger.LogInformation("=== AI Stock Trade MCP Server Starting ===");
logger.LogInformation("Transport Mode: {TransportMode} {AutoDetected}", 
    useStreamableHttp ? "HTTP" : "STDIO",
    (Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") != null || 
     Environment.GetEnvironmentVariable("CONTAINER_APP_NAME") != null ||
     Environment.GetEnvironmentVariable("WEBSITES_PORT") != null ||
     Environment.GetEnvironmentVariable("PORT") != null) ? "(Auto-detected Azure environment)" : "");
logger.LogInformation("API Base URL: {ApiBaseUrl}", apiBaseUrl);
logger.LogInformation("Application Insights: {AppInsightsStatus}", 
    !string.IsNullOrEmpty(appInsightsKey) || !string.IsNullOrEmpty(appInsightsConnectionString) ? "Enabled" : "Disabled");

if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    logger.LogInformation("Application Insights Connection String configured: {ConnectionStringPrefix}...", 
        appInsightsConnectionString.Length > 50 ? appInsightsConnectionString.Substring(0, 50) : appInsightsConnectionString);
}
else if (!string.IsNullOrEmpty(appInsightsKey))
{
    logger.LogInformation("Application Insights Instrumentation Key configured: {KeyPrefix}...", 
        appInsightsKey.Length > 8 ? appInsightsKey.Substring(0, 8) : appInsightsKey);
}

logger.LogInformation("Environment: {Environment}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development");
logger.LogInformation("Available tools: StockTradingTools (GetStockQuote, GetHistoricalData, SearchStockSymbols, GetStockDetails, GetListedStocks, GetRandomListedStock, GetDetailedHistoricalPrices, GetSystemStatus), RandomNumberTools (GetRandomNumber, GetRandomNumberList)");

// Track startup event in Application Insights
telemetryClient.TrackEvent("McpServer.Startup", new Dictionary<string, string>
{
    ["TransportMode"] = useStreamableHttp ? "HTTP" : "STDIO",
    ["ApiBaseUrl"] = apiBaseUrl,
    ["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
    ["ApplicationInsightsEnabled"] = (!string.IsNullOrEmpty(appInsightsKey) || !string.IsNullOrEmpty(appInsightsConnectionString)).ToString(),
    ["Version"] = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown"
});

if (useStreamableHttp)
{
    var portEnv = Environment.GetEnvironmentVariable("PORT")
                  ?? Environment.GetEnvironmentVariable("WEBSITES_PORT")
                  ?? Environment.GetEnvironmentVariable("ASPNETCORE_PORT");
    
    var configuredPort = !string.IsNullOrEmpty(portEnv) ? portEnv : "default";
    logger.LogInformation("HTTP MCP Server configured for port: {Port}", configuredPort);
    logger.LogInformation("Health endpoint available at: /health");
    logger.LogInformation("MCP endpoint available at: /mcp");
    logger.LogInformation("Test with: curl -X POST http://localhost:{Port}/mcp -H \"Accept: application/json, text/event-stream\" -H \"Content-Type: application/json\" -d '{{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}}'", configuredPort);
    
    telemetryClient.TrackEvent("McpServer.HttpModeStartup", new Dictionary<string, string>
    {
        ["Port"] = configuredPort,
        ["HealthEndpoint"] = "/health",
        ["McpEndpoint"] = "/mcp"
    });
}
else
{
    logger.LogInformation("STDIO MCP Server ready for protocol communication");
    telemetryClient.TrackEvent("McpServer.StdioModeStartup");
}

logger.LogInformation("=== AI Stock Trade MCP Server Started Successfully ===");

await app.RunAsync();
