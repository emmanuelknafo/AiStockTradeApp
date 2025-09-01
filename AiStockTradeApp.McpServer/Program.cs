using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration from appsettings.json and environment variables
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Register HTTP client for API calls
builder.Services.AddHttpClient<StockTradingTools>();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<StockTradingTools>()
    .WithTools<RandomNumberTools>(); // Keep the sample tool for reference

var host = builder.Build();

// Log startup information
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var configuration = host.Services.GetRequiredService<IConfiguration>();
var apiBaseUrl = configuration["STOCK_API_BASE_URL"] ?? 
                Environment.GetEnvironmentVariable("STOCK_API_BASE_URL") ?? 
                "http://localhost:5000";

logger.LogInformation("Starting AI Stock Trade MCP Server");
logger.LogInformation("API Base URL: {ApiBaseUrl}", apiBaseUrl);
logger.LogInformation("Available tools: StockTradingTools, RandomNumberTools");

await host.RunAsync();
