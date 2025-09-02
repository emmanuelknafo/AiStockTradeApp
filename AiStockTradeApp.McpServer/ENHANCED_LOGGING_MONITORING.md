# MCP Server Enhanced Logging and Application Insights Integration

## Overview

The AI Stock Trade MCP Server now includes comprehensive logging and Application Insights integration to provide detailed monitoring and telemetry for tool usage by MCP consumers (Claude, ChatGPT, VS Code, etc.).

## Features Added

### 1. Application Insights Integration

**Packages Added:**
- `Microsoft.ApplicationInsights.AspNetCore` - Core Application Insights for ASP.NET Core
- `Microsoft.ApplicationInsights.DependencyCollector` - HTTP dependency tracking
- `Microsoft.ApplicationInsights.EventCounterCollector` - Performance counter collection
- `Microsoft.ApplicationInsights.PerfCounterCollector` - System performance counters
- `Microsoft.Extensions.Logging.ApplicationInsights` - Integration with ILogger

**Configuration:**
```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "your-key-here",
    "ConnectionString": "InstrumentationKey=your-key-here;IngestionEndpoint=...",
    "EnableAdaptiveSampling": true,
    "EnableQuickPulseMetricStream": true,
    "EnablePerformanceCounterCollectionModule": true,
    "EnableEventCounterCollectionModule": true,
    "EnableDependencyTrackingTelemetryModule": true
  }
}
```

### 2. Telemetry Middleware (`McpTelemetryMiddleware`)

**Purpose:** Tracks all MCP requests and tool usage with detailed telemetry.

**Features:**
- **Consumer Identification:** Automatically detects MCP clients (Claude Desktop, ChatGPT, Cursor IDE, VS Code, etc.)
- **Request Tracking:** Captures request details, duration, status codes
- **Tool Usage Analytics:** Tracks which tools are called with what parameters
- **Error Tracking:** Comprehensive exception handling and reporting
- **Performance Monitoring:** Response times and payload sizes

**Tracked Events:**
- `MCP Tool Called` - When a specific tool is invoked
- `MCP Tools Listed` - When client requests available tools
- `MCP Method Called` - For other MCP protocol methods
- `MCP Request` - Complete request telemetry

**Tracked Metrics:**
- `MCP.Tool.{ToolName}.Usage` - Usage count per tool
- `MCP.Response.Size` - Response payload sizes
- Request durations and success rates

### 3. Enhanced Tool Logging

**Each tool now includes:**
- **Structured Logging:** Consistent log messages with correlation IDs
- **Performance Tracking:** Start/end times and duration measurement
- **Parameter Validation:** Input validation with specific error tracking
- **Success/Failure Metrics:** Detailed outcome tracking
- **API Dependency Tracking:** External API call monitoring

**Example Log Output:**
```
[2025-09-02 14:30:15.123] Information: GetStockQuote started for symbol: AAPL
[2025-09-02 14:30:15.125] Debug: Making API request for stock quote: AAPL to http://localhost:5000/api/stocks/quote?symbol=AAPL
[2025-09-02 14:30:15.342] Information: GetStockQuote completed successfully for AAPL - Price: 150.25, Change: 1.5%, Duration: 219ms
```

### 4. Consumer Identification

**Automatic Detection of MCP Clients:**
- **Claude Desktop** - Detected via User-Agent patterns
- **ChatGPT** - Identified through request headers
- **Cursor IDE** - Recognized from User-Agent
- **VS Code** - Detected via User-Agent
- **Custom Clients** - Support for `X-MCP-Consumer` header

### 5. Custom Telemetry Events

**Tool-Specific Events:**
- `StockQuote.Success/ApiError/Exception`
- `HistoricalData.Success/ApiError/Exception`
- `StockSearch.Success/ApiError/Exception`
- `RandomNumber.Generated`

**Server Events:**
- `McpServer.Startup`
- `McpServer.HttpModeStartup`
- `McpServer.StdioModeStartup`

## Configuration

### Environment Variables

```bash
# Application Insights
APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..."
APPLICATIONINSIGHTS_INSTRUMENTATION_KEY="your-instrumentation-key"

# Stock API
STOCK_API_BASE_URL="https://your-api-endpoint.com"

# Runtime Environment
ASPNETCORE_ENVIRONMENT="Production"
```

### Azure Configuration

**For Azure App Service:**
```bash
az webapp config appsettings set --resource-group myResourceGroup --name myApp --settings \
  APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..." \
  STOCK_API_BASE_URL="https://myapi.azurewebsites.net"
```

**For Azure Container Apps:**
```bash
az containerapp update --name myapp --resource-group myResourceGroup \
  --set-env-vars APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..."
```

## Monitoring Dashboards

### Key Metrics to Monitor

1. **Tool Usage:**
   - `MCP.Tool.GetStockQuote.Usage`
   - `MCP.Tool.GetHistoricalData.Usage`
   - `MCP.Tool.SearchStockSymbols.Usage`

2. **Performance:**
   - `StockQuote.ResponseTime`
   - `HistoricalData.ResponseTime`
   - `StockSearch.ResponseTime`

3. **Consumer Analytics:**
   - Requests by consumer type (Claude, ChatGPT, etc.)
   - Usage patterns and peak times
   - Error rates by consumer

4. **API Dependencies:**
   - External API response times
   - API failure rates
   - Dependency availability

### Sample KQL Queries

**Tool Usage by Consumer:**
```kql
customEvents
| where name == "MCP Tool Called"
| extend Consumer = tostring(customDimensions.Consumer)
| extend ToolName = tostring(customDimensions.ToolName)
| summarize Count = count() by Consumer, ToolName
| order by Count desc
```

**Performance Analysis:**
```kql
customMetrics
| where name contains "ResponseTime"
| extend Tool = extract(@"(\w+)\.ResponseTime", 1, name)
| summarize avg(value), max(value), min(value) by Tool
```

**Error Tracking:**
```kql
exceptions
| where customDimensions contains "MCP"
| extend Method = tostring(customDimensions.Method)
| extend Consumer = tostring(customDimensions.Consumer)
| summarize Count = count() by Method, type, Consumer
```

## Development Usage

### Local Development

1. **Run with detailed logging:**
   ```bash
   dotnet run --project AiStockTradeApp.McpServer
   ```

2. **Enable Application Insights locally:**
   ```json
   // appsettings.Development.json
   {
     "ApplicationInsights": {
       "ConnectionString": "your-dev-connection-string"
     }
   }
   ```

### Testing Telemetry

**Test tool usage:**
```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -H "X-MCP-Consumer: TestClient" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/call",
    "params": {
      "name": "GetStockQuote",
      "arguments": {
        "symbol": "AAPL"
      }
    }
  }'
```

## Benefits

1. **Operational Visibility:** Complete insight into how consumers use the MCP server
2. **Performance Monitoring:** Track response times and identify bottlenecks
3. **Consumer Analytics:** Understand which AI clients use which tools most
4. **Error Tracking:** Comprehensive exception handling and alerting
5. **Capacity Planning:** Usage patterns help with scaling decisions
6. **Debugging:** Detailed logs help troubleshoot integration issues

## Alerting Recommendations

### Critical Alerts
- **High Error Rate:** > 5% exceptions in 5-minute window
- **Slow Performance:** Average response time > 2 seconds
- **API Dependency Failures:** External API error rate > 10%

### Warning Alerts
- **Unusual Usage Patterns:** 5x increase in tool usage
- **Consumer Errors:** Specific consumer experiencing high error rates
- **Resource Usage:** High memory or CPU utilization

This enhanced monitoring provides comprehensive visibility into MCP server operations and consumer behavior, enabling proactive monitoring and optimization.
