# AI Stock Trade MCP Server - Dual Transport Implementation

## Overview

This MCP (Model Context Protocol) server provides AI assistants with access to stock trading operations through a comprehensive set of tools. The server implements **dual transport support** - it can run in both **STDIO mode** (for AI assistants) and **HTTP mode** (for testing and web-based integrations) using a single `--http` switch.

## Architecture

The implementation follows Microsoft's recommended pattern for dual transport MCP servers, as outlined in their [technical blog post](https://techcommunity.microsoft.com/blog/azuredevcommunityblog/one-mcp-server-two-transports-stdio-and-http/4443915).

### Key Features

- ✅ **Dual Transport Support**: STDIO for AI assistants, HTTP for testing/web integration
- ✅ **Stock Trading Tools**: 7 comprehensive stock market tools
- ✅ **Containerization Ready**: Docker support with multi-stage builds
- ✅ **Environment Configuration**: Configurable API endpoints via environment variables
- ✅ **Error Handling**: Robust error handling and logging
- ✅ **Testing Support**: Built-in test scripts and HTTP endpoints

## Transport Modes

### STDIO Mode (Default)
Used by AI assistants like GitHub Copilot, Claude Desktop, and other MCP clients.

```bash
dotnet run
```

### HTTP Mode
Used for testing, debugging, and web-based integrations.

```bash
dotnet run -- --http
```

The HTTP server runs on `http://localhost:5000/mcp` and accepts JSON-RPC requests.

## Available Tools

| Tool | Description | Parameters |
|------|-------------|------------|
| **GetStockQuote** | Get real-time stock quote data | `symbol`: Stock symbol (e.g., AAPL) |
| **GetHistoricalData** | Get historical price data for analysis | `symbol`: Stock symbol, `days`: Number of days |
| **GetListedStocks** | Get paginated list of listed stocks | `page`: Page number, `pageSize`: Items per page |
| **SearchStockSymbols** | Search for stocks by name or symbol | `query`: Search query |
| **GetStockDetails** | Get detailed company information | `symbol`: Stock symbol |
| **GetDetailedHistoricalPrices** | Get comprehensive historical data | `symbol`: Stock symbol, `days`: Number of days |
| **GetSystemStatus** | Get API system status and health | None |
| **GetRandomNumber** | Generate random number (sample tool) | `min`: Minimum value, `max`: Maximum value |

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- Stock Trading API running at `https://app-aistock-dev-002.azurewebsites.net` (or configure `STOCK_API_BASE_URL`)

### 1. Clone and Build

```bash
git clone <repository>
cd AiStockTradeApp/AiStockTradeApp.McpServer
dotnet build
```

### 2. Configure Environment

Set the stock API base URL:

```bash
# Windows PowerShell
$env:STOCK_API_BASE_URL = "https://app-aistock-dev-002.azurewebsites.net"

# Linux/macOS
export STOCK_API_BASE_URL="https://app-aistock-dev-002.azurewebsites.net"
```

### 3. Run in STDIO Mode (for AI assistants)

```bash
dotnet run
```

### 4. Run in HTTP Mode (for testing)

```bash
dotnet run -- --http
```

## Testing the HTTP Mode

### Test Script

Run the included PowerShell test script:

```powershell
.\test-http-mode.ps1
```

### Manual Testing with curl

1. **List available tools:**
```bash
curl -X POST http://localhost:5000/mcp \
  -H "Accept: application/json, text/event-stream" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

2. **Get stock quote:**
```bash
curl -X POST http://localhost:5000/mcp \
  -H "Accept: application/json, text/event-stream" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"GetStockQuote","arguments":{"symbol":"AAPL"}}}'
```

3. **Get top 10 listed stocks:**
```bash
curl -X POST http://localhost:5000/mcp \
  -H "Accept: application/json, text/event-stream" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"GetListedStocks","arguments":{"count":10}}}'
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `STOCK_API_BASE_URL` | Base URL for the stock trading API | `http://localhost:5000` |
| `UseHttp` | Set to `true` to enable HTTP mode | `false` |

### Configuration File (appsettings.json)

```json
{
  "STOCK_API_BASE_URL": "https://localhost:7032",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

## Docker Deployment

### Build Container

```bash
# From the solution root directory
docker build -f AiStockTradeApp.McpServer/Dockerfile -t ai-stock-mcp-server .
```

### Run in STDIO Mode

```bash
docker run -it --rm \
  -e STOCK_API_BASE_URL=https://your-api-host:7032 \
  ai-stock-mcp-server
```

### Run in HTTP Mode

```bash
docker run -d --rm \
  -p 5000:5000 \
  -e STOCK_API_BASE_URL=https://your-api-host:7032 \
  ai-stock-mcp-server \
  dotnet AiStockTradeApp.McpServer.dll --http
```

## VS Code MCP Configuration

Add to your VS Code `mcp.json`:

```json
{
  "mcpServers": {
    "StockTrading": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "path/to/AiStockTradeApp.McpServer"
      ],
      "env": {
        "STOCK_API_BASE_URL": "https://localhost:7032"
      }
    }
  }
}
```

## Development

### Project Structure

```
AiStockTradeApp.McpServer/
├── Program.cs                 # Dual transport implementation
├── Tools/
│   ├── StockTradingTools.cs   # Stock trading MCP tools
│   └── RandomNumberTools.cs   # Sample tool
├── Dockerfile                 # Container configuration
├── test-http-mode.ps1         # HTTP testing script
└── README.md                  # This file
```

### Key Implementation Details

1. **Transport Selection**: Uses command-line argument parsing to detect `--http` switch
2. **Builder Pattern**: Leverages .NET builder pattern to create either `WebApplication` or `Host`
3. **Conditional Configuration**: Applies transport-specific configurations based on mode
4. **Shared Services**: Both transports share the same MCP tools and dependencies

### Code Architecture

```csharp
// Check transport mode
var useStreamableHttp = UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

// Create appropriate builder
IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

// Configure MCP server with conditional transport
var mcpServerBuilder = builder.Services
    .AddMcpServer()
    .WithTools<StockTradingTools>()
    .WithTools<RandomNumberTools>();

if (useStreamableHttp)
{
    mcpServerBuilder.WithHttpTransport(o => o.Stateless = true);
}
else
{
    mcpServerBuilder.WithStdioServerTransport();
}
```

## Troubleshooting

### Common Issues

1. **Port Already in Use**
   ```bash
   # Check what's using port 5000
   netstat -an | findstr :5000
   
   # Kill process if needed
   taskkill /PID <process_id> /F
   ```

2. **API Connection Issues**
   - Verify the stock trading API is running
   - Check the `STOCK_API_BASE_URL` environment variable
   - Ensure firewall/network connectivity

3. **Build Errors**
   - Ensure .NET 9.0 SDK is installed
   - Check for running processes locking the executable
   - Restore NuGet packages: `dotnet restore`

### Debug Logging

Enable detailed logging by setting environment variable:
```bash
export ASPNETCORE_ENVIRONMENT=Development
```

## References

- [Microsoft MCP Dual Transport Blog Post](https://techcommunity.microsoft.com/blog/azuredevcommunityblog/one-mcp-server-two-transports-stdio-and-http/4443915)
- [MCP Samples in .NET](https://aka.ms/mcp/dotnet/samples)
- [Model Context Protocol Specification](https://spec.modelcontextprotocol.io/)
- [AI Stock Trade App Repository](https://github.com/emmanuelknafo/AiStockTradeApp)
