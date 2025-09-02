# AI Stock Trade App - MCP Server

A Model Context Protocol (MCP) server that provides AI assistants with access to real-time stock market data and trading tools through the AI Stock Trade App API.

## 🚀 Overview

This MCP server enables external AI assistants (Claude, ChatGPT, or other MCP-compatible clients) to access comprehensive stock trading functionality including real-time quotes, historical data, stock search, and system monitoring.

## 🛠️ Available Tools

### Stock Data Tools
- **GetStockQuote** - Get real-time stock quote data including current price, change, and basic company information
- **GetHistoricalData** - Retrieve historical price data over a specified number of days
- **GetDetailedHistoricalPrices** - Get detailed historical prices with optional date range filtering
- **SearchStockSymbols** - Search for stock symbols by company name or ticker
- **GetStockDetails** - Get detailed company information including sector and industry
- **GetListedStocks** - Browse available stocks with pagination support
- **GetRandomListedStock** - Get a random listed stock for discovery and exploration of new investment opportunities
- **GetSystemStatus** - Check API system health and availability

### Configuration Requirements

The MCP server requires the Stock Trading API to be running and accessible. Configure the API base URL through:
- Environment variable: `STOCK_API_BASE_URL`
- Configuration input during setup
- Default: `http://localhost:5000`

## 🔧 Local Development Setup

To test this MCP server from source code (locally) without using a built MCP server package, you can configure your IDE to run the project directly using `dotnet run`.

```json
{
  "inputs": [
    {
      "type": "promptString",
      "id": "stock_api_base_url",
      "description": "Base URL for the Stock Trading API (e.g., http://localhost:5000)",
      "password": false
    }
  ],
  "servers": {
    "AiStockTradeApp.McpServer": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "<PATH TO PROJECT DIRECTORY>"
      ],
      "env": {
        "STOCK_API_BASE_URL": "${input:stock_api_base_url}"
      }
    }
  }
}
```

## Testing the MCP Server

Once configured, you can test both the sample tools and stock trading functionality:

**Sample Tools:**
- Ask for random numbers: `Give me 3 random numbers`

**Stock Trading Tools:**
- Get stock quotes: `Get the current stock quote for Apple (AAPL)`
- Historical data: `Show me 30 days of historical data for Microsoft`
- Search symbols: `Search for Tesla stock symbol`
- System status: `Check the stock trading system status`

The MCP server should prompt you to use the appropriate tools and show you the results. Make sure your Stock Trading API is running and accessible at the configured URL.

## Publishing to NuGet.org

1. Run `dotnet pack -c Release` to create the NuGet package
2. Publish to NuGet.org with `dotnet nuget push bin/Release/*.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json`

## Using the MCP Server from NuGet.org

Once the MCP server package is published to NuGet.org, you can configure it in your preferred IDE. Both VS Code and Visual Studio use the `dnx` command to download and install the MCP server package from NuGet.org.

- **VS Code**: Create a `<WORKSPACE DIRECTORY>/.vscode/mcp.json` file
- **Visual Studio**: Create a `<SOLUTION DIRECTORY>\.mcp.json` file

For both VS Code and Visual Studio, the configuration file uses the following server definition:

```json
{
  "servers": {
    "AiStockTradeApp.McpServer": {
      "type": "stdio",
      "command": "dnx",
      "args": [
        "<your package ID here>",
        "--version",
        "<your package version here>",
        "--yes"
      ]
    }
  }
}
```

## More information

.NET MCP servers use the [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) C# SDK. For more information about MCP:

- [Official Documentation](https://modelcontextprotocol.io/)
- [Protocol Specification](https://spec.modelcontextprotocol.io/)
- [GitHub Organization](https://github.com/modelcontextprotocol)

Refer to the VS Code or Visual Studio documentation for more information on configuring and using MCP servers:

- [Use MCP servers in VS Code (Preview)](https://code.visualstudio.com/docs/copilot/chat/mcp-servers)
- [Use MCP servers in Visual Studio (Preview)](https://learn.microsoft.com/visualstudio/ide/mcp-servers)
