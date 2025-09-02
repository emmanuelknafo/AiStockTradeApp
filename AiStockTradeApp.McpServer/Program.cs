using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using ModelContextProtocol.AspNetCore;
using System.Collections;

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

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages in STDIO mode).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

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
var apiBaseUrl = configuration["STOCK_API_BASE_URL"] ?? 
                Environment.GetEnvironmentVariable("STOCK_API_BASE_URL") ?? 
                "http://localhost:5000"; // Default to localhost for development

logger.LogInformation("Starting AI Stock Trade MCP Server");
logger.LogInformation("Transport Mode: {TransportMode} {AutoDetected}", 
    useStreamableHttp ? "HTTP" : "STDIO",
    (Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") != null || 
     Environment.GetEnvironmentVariable("CONTAINER_APP_NAME") != null ||
     Environment.GetEnvironmentVariable("WEBSITES_PORT") != null ||
     Environment.GetEnvironmentVariable("PORT") != null) ? "(Auto-detected Azure environment)" : "");
logger.LogInformation("API Base URL: {ApiBaseUrl}", apiBaseUrl);
logger.LogInformation("Available tools: StockTradingTools (GetStockQuote, GetHistoricalData, SearchStockSymbols, GetStockDetails, GetListedStocks, GetRandomListedStock, GetDetailedHistoricalPrices, GetSystemStatus), RandomNumberTools");

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
}

await app.RunAsync();
