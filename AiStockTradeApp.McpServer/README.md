# AiStockTradeApp.McpServer - Model Context Protocol Server

## 🚀 Project Overview

A Model Context Protocol (MCP) server that provides external AI assistants (Claude, ChatGPT, or other MCP-compatible clients) with access to comprehensive stock trading functionality through the AI Stock Trade App API.

## 🏗️ Architecture Role

This project serves as an **integration bridge** between external AI assistants and the stock trading application:

- **Protocol Implementation** - Full MCP specification compliance
- **Tool Registration** - Automatic discovery of stock trading tools
- **HTTP Client Integration** - Communicates with AiStockTradeApp.Api
- **External AI Access** - Enables AI assistants to access stock data
- **Dual Transport Support** - STDIO and HTTP communication protocols

### Key Responsibilities

- **Stock data access** for external AI assistants
- **Real-time market information** retrieval
- **Historical data analysis** capabilities
- **Stock search and discovery** tools
- **System health monitoring** for AI clients
- **Utility functions** for data generation and analysis

## 🛠️ Available MCP Tools

### Stock Data Tools

- **GetStockQuote** - Real-time stock quote data including price, change, and company information
- **GetHistoricalData** - Historical price data over a specified number of days (up to 365 days)
- **GetDetailedHistoricalPrices** - Detailed historical prices with optional date range filtering
- **SearchStockSymbols** - Search for stock symbols by company name or ticker
- **GetStockDetails** - Detailed company information including sector, industry, and market cap
- **GetListedStocks** - Browse available stocks with pagination support
- **GetRandomListedStock** - Get a random stock for investment discovery and exploration
- **GetSystemStatus** - Check API system health and availability status

### Utility Tools

- **GetRandomNumber** - Generate a single random number within a specified range
- **GetRandomNumberList** - Generate lists of random numbers with customizable parameters

## 🔧 Configuration

### Environment Variables

The MCP server requires configuration of the Stock Trading API endpoint:

- **STOCK_API_BASE_URL** - Base URL for the Stock Trading API
  - Default: `http://localhost:5000`
  - Production: `https://your-api-domain.com`

### MCP Client Configuration

#### For Claude Desktop

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
        "C:\\path\\to\\AiStockTradeApp.McpServer\\AiStockTradeApp.McpServer.csproj"
      ],
      "env": {
        "STOCK_API_BASE_URL": "${input:stock_api_base_url}"
      }
    }
  }
}
```

#### For Other MCP Clients

```json
{
  "servers": {
    "stock-trading": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/AiStockTradeApp.McpServer"],
      "env": {
        "STOCK_API_BASE_URL": "http://localhost:5000"
      }
    }
  }
}
```

## 🚀 Development Setup

### Prerequisites

- **.NET 9 SDK** - Latest version
- **AiStockTradeApp.Api** - Must be running on configured port
- **MCP-compatible client** - Claude Desktop, compatible AI assistant, or testing tool

### Local Development

1. **Start the API backend**

   ```bash
   # Start the main API (required dependency)
   dotnet run --project AiStockTradeApp.Api
   ```

2. **Configure environment**

   ```bash
   # Set API base URL (PowerShell)
   $env:STOCK_API_BASE_URL = "http://localhost:5000"
   
   # Set API base URL (Bash)
   export STOCK_API_BASE_URL="http://localhost:5000"
   ```

3. **Run the MCP server**

   ```bash
   # STDIO mode (default for MCP clients)
   dotnet run --project AiStockTradeApp.McpServer
   
   # HTTP mode (for testing)
   dotnet run --project AiStockTradeApp.McpServer --urls "http://localhost:8080"
   ```

### Testing the Server

```bash
# Test API connectivity
curl $STOCK_API_BASE_URL/health

# Test MCP server (HTTP mode)
curl http://localhost:8080/health

# View available tools (HTTP mode)
curl http://localhost:8080/tools
```

## � Project Structure

AiStockTradeApp.McpServer/
├── Tools/
│   ├── StockTradingTools.cs        # MCP tool implementations
│   └── UtilityTools.cs             # Utility tool implementations
├── Models/
│   ├── StockModels.cs              # Stock data models for MCP
│   └── ToolModels.cs               # Tool parameter and response models
├── Services/
│   ├── StockApiClient.cs           # HTTP client for API communication
│   └── McpConfigurationService.cs # MCP server configuration
├── Program.cs                      # Application entry point and MCP setup
├── appsettings.json                # Application configuration
├── appsettings.Development.json    # Development-specific settings
├── Dockerfile                      # Container configuration
├── build.ps1                       # Build automation script
├── deploy-azure.ps1                # Azure deployment script
└── claude-config-example.md        # Claude Desktop configuration guide

## 🔗 Dependencies

### Project References

- **HTTP Client** - Direct HTTP communication with AiStockTradeApp.Api
- **No direct project references** - Loose coupling through HTTP API

### NuGet Packages

```xml
<PackageReference Include="MCP.NET" />
<PackageReference Include="Microsoft.Extensions.Http" />
<PackageReference Include="Microsoft.Extensions.Logging" />
<PackageReference Include="System.Text.Json" />
```

## 🌐 Tool Usage Examples

### Stock Quote Retrieval

```json
{
  "tool": "GetStockQuote",
  "arguments": {
    "symbol": "AAPL"
  }
}
```

**Response:**

```json
{
  "symbol": "AAPL",
  "currentPrice": 150.25,
  "change": 2.15,
  "changePercent": 1.45,
  "companyName": "Apple Inc.",
  "lastUpdated": "2025-09-02T14:30:00Z"
}
```

### Historical Data Analysis

```json
{
  "tool": "GetHistoricalData",
  "arguments": {
    "symbol": "GOOGL",
    "days": 30
  }
}
```

### Stock Symbol Search

```json
{
  "tool": "SearchStockSymbols",
  "arguments": {
    "query": "Apple"
  }
}
```

### System Health Check

```json
{
  "tool": "GetSystemStatus",
  "arguments": {}
}
```

## 🔄 Integration Patterns

### HTTP Client Configuration

```csharp
// StockApiClient.cs
public class StockApiClient
{
    private readonly HttpClient _httpClient;
    
    public StockApiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var baseUrl = configuration["STOCK_API_BASE_URL"] ?? "http://localhost:5000";
        _httpClient.BaseAddress = new Uri(baseUrl);
    }
    
    public async Task<StockQuote> GetStockQuoteAsync(string symbol)
    {
        var response = await _httpClient.GetAsync($"/api/stocks/quote?symbol={symbol}");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<StockQuote>(json);
    }
}
```

### MCP Tool Implementation

```csharp
// StockTradingTools.cs
[McpTool]
public class StockTradingTools
{
    private readonly StockApiClient _apiClient;
    
    [McpToolMethod("GetStockQuote")]
    public async Task<object> GetStockQuoteAsync(
        [McpToolParameter("Stock symbol (e.g., AAPL, GOOGL)")] string symbol)
    {
        try
        {
            var quote = await _apiClient.GetStockQuoteAsync(symbol.ToUpper());
            return new
            {
                success = true,
                data = quote
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = ex.Message
            };
        }
    }
}
```

## 🐳 Containerization

### Docker Support

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["AiStockTradeApp.McpServer/AiStockTradeApp.McpServer.csproj", "AiStockTradeApp.McpServer/"]
RUN dotnet restore "AiStockTradeApp.McpServer/AiStockTradeApp.McpServer.csproj"
```

### Docker Compose Integration

```yaml
# docker-compose.mcp-override.yml
services:
  mcp-server:
    build:
      context: .
      dockerfile: AiStockTradeApp.McpServer/Dockerfile
    environment:
      - STOCK_API_BASE_URL=http://api:80
    ports:
      - "8080:8080"
    depends_on:
      - api
```

## 🧪 Testing

### Manual Testing

```bash
# Test with curl (HTTP mode)
curl -X POST http://localhost:8080/mcp \
  -H "Content-Type: application/json" \
  -d '{"method": "tools/call", "params": {"name": "GetStockQuote", "arguments": {"symbol": "AAPL"}}}'
```

### Integration Testing

```csharp
[Fact]
public async Task GetStockQuote_ValidSymbol_ReturnsData()
{
    // Arrange
    var tools = new StockTradingTools(_mockApiClient);
    
    // Act
    var result = await tools.GetStockQuoteAsync("AAPL");
    
    // Assert
    result.Should().NotBeNull();
}
```

## 🔒 Security Considerations

### Network Security

- **HTTPS enforcement** - Production API communication
- **Input validation** - All tool parameters validated
- **Error handling** - No sensitive information exposure
- **Rate limiting** - Client-side request throttling

### Authentication

- **API key management** - Secure credential handling
- **Environment variables** - Configuration security
- **No direct database access** - API-mediated data access

## ☁️ Azure Deployment

### Azure Container Apps

```bash
# Deploy to Azure Container Apps
.\deploy-azure.ps1 -ResourceGroupName "stock-trading-rg" -Location "East US"
```

### Environment Configuration

```json
{
  "environmentVariables": [
    {
      "name": "STOCK_API_BASE_URL",
      "value": "https://your-api.azurewebsites.net"
    },
    {
      "name": "ASPNETCORE_ENVIRONMENT",
      "value": "Production"
    }
  ]
}
```

## 🔧 Troubleshooting

### Common Issues

#### API Connection Problems

```bash
# Test API connectivity
curl $STOCK_API_BASE_URL/health

# Check environment variables
echo $STOCK_API_BASE_URL
```

#### MCP Client Issues

- **STDIO communication** - Ensure proper command configuration
- **Path resolution** - Use absolute paths in MCP client config
- **Environment variables** - Verify all required variables are set

#### Tool Execution Errors

- **Parameter validation** - Check tool parameter formats
- **API rate limits** - Monitor external API quotas
- **Network connectivity** - Test HTTP client connectivity

## 📊 Usage Analytics

### Logging and Monitoring

```csharp
// Program.cs - Logging configuration
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddApplicationInsights();
});
```

### Performance Metrics

- **Tool execution time** - Response time monitoring
- **API call frequency** - Usage pattern analysis
- **Error rates** - Tool failure tracking
- **Client connections** - MCP client usage statistics

## 🤝 External AI Integration

### Supported AI Clients

- **Claude Desktop** - Full MCP protocol support
- **ChatGPT with MCP** - Tool-based integration
- **Custom MCP clients** - Standard protocol compliance
- **Development tools** - MCP testing utilities

### Usage Scenarios

- **Investment research** - AI-powered stock analysis
- **Portfolio management** - Automated portfolio optimization
- **Market monitoring** - Real-time data access for AI agents
- **Trading automation** - AI-driven trading decisions
