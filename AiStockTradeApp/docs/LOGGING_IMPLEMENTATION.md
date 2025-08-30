# Logging Implementation - AiStockTradeApp UI

## Overview

The AiStockTradeApp UI project now includes comprehensive logging for monitoring, debugging, and observability. This implementation follows ASP.NET Core logging best practices and integrates with Azure Application Insights.

## Features

### 🔍 Request/Response Logging
- **Custom middleware** (`RequestLoggingMiddleware`) tracks all HTTP requests
- **Correlation IDs** for tracing requests across services
- **Request duration** timing for performance monitoring
- **Error tracking** with full exception details
- **Header logging** (excluding sensitive headers like authorization)

### 📊 Structured Logging
- **Consistent log templates** using structured logging patterns
- **Performance metrics** with operation timing
- **Business event tracking** for watchlist changes and user actions
- **External API call monitoring** with success/failure rates
- **Database operation tracking** (when applicable)

### 🎯 Log Levels and Categories

#### Development Environment
- **Debug**: Detailed execution flow, API calls, data retrieval
- **Information**: User actions, business events, performance metrics
- **Warning**: Non-critical issues, fallback scenarios
- **Error**: Exceptions and critical failures

#### Production Environment
- **Information**: User actions, business events, system health
- **Warning**: Performance issues, external API failures
- **Error**: Critical failures requiring investigation

### 📁 Logging Configuration

#### appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "AiStockTradeApp": "Debug",
      "AiStockTradeApp.Controllers": "Debug",
      "AiStockTradeApp.Services": "Debug",
      "AiStockTradeApp.Middleware": "Debug"
    }
  }
}
```

#### appsettings.Production.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "AiStockTradeApp": "Information",
      "AiStockTradeApp.Middleware": "Warning"
    },
    "ApplicationInsights": {
      "LogLevel": {
        "Default": "Information",
        "AiStockTradeApp": "Information"
      }
    }
  }
}
```

## 🛠️ Components

### RequestLoggingMiddleware
- **Location**: `Middleware/RequestLoggingMiddleware.cs`
- **Purpose**: Automatic request/response logging with timing
- **Features**:
  - Request start/completion logging
  - Duration tracking with Stopwatch
  - Exception handling and logging
  - Header inspection (non-sensitive only)
  - Correlation ID propagation

### LoggingExtensions
- **Location**: `Services/LoggingExtensions.cs`
- **Purpose**: Structured logging helper methods
- **Features**:
  - Operation timing with disposable pattern
  - Database operation tracking
  - HTTP request logging
  - User action logging
  - Business event logging
  - Performance metric tracking

### Enhanced Controllers
#### StockController
- **Dashboard loading** with comprehensive logging
- **Stock addition/removal** with user action tracking
- **Performance monitoring** for data retrieval operations
- **Error handling** with detailed context

#### HomeController
- **Navigation tracking** for user flow analysis
- **Localization debugging** with culture information
- **Language switching** with user preference logging

### Enhanced Services
#### ApiStockDataServiceClient
- **API call logging** with timing and success/failure tracking
- **Fallback mechanism** logging for service resilience
- **HTTP request/response** detailed tracking
- **Error categorization** (network vs application errors)

## 🔧 Usage Examples

### Basic Logging in Controllers
```csharp
public class MyController : Controller
{
    private readonly ILogger<MyController> _logger;

    public async Task<IActionResult> MyAction(string parameter)
    {
        using var operation = _logger.LogOperationStart("MyAction", new { Parameter = parameter });
        
        try
        {
            _logger.LogUserAction("ActionStarted", GetSessionId(), new { Parameter = parameter });
            
            // Business logic here
            var result = await _myService.ProcessAsync(parameter);
            
            _logger.LogBusinessEvent("ActionCompleted", new { Parameter = parameter, Result = result });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MyAction with parameter {Parameter}", parameter);
            return StatusCode(500, "Internal server error");
        }
    }
}
```

### Logging in Services
```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public async Task<string> ProcessAsync(string input)
    {
        using var dbOperation = _logger.LogDatabaseOperation("Select", "Entity", input);
        
        _logger.LogHttpRequest("GET", $"/api/external/{input}");
        
        // Process logic
        
        _logger.LogPerformanceMetric("ProcessingTime", stopwatch.ElapsedMilliseconds, "ms");
        return result;
    }
}
```

## 🔍 Monitoring and Observability

### Application Insights Integration
- **Automatic telemetry** collection for requests, dependencies, exceptions
- **Custom events** for business logic tracking
- **Performance counters** for system health monitoring
- **Log streaming** for real-time debugging

### Key Metrics Tracked
1. **Request Duration**: Time taken for each HTTP request
2. **API Call Success Rate**: External API reliability
3. **User Actions**: Watchlist modifications, dashboard access
4. **Error Rates**: Exception frequency by category
5. **Cache Performance**: Hit/miss ratios (when applicable)

### Correlation and Tracing
- **Correlation IDs** automatically generated for each request
- **Operation context** maintained across async operations
- **Service boundaries** clearly marked in logs
- **User session tracking** for flow analysis

## 🚨 Troubleshooting

### Common Log Patterns

#### Finding User Issues
```bash
# Search for user-specific errors
Application Insights: traces | where customDimensions.SessionId == "session-123"

# Find slow operations
traces | where customDimensions.Duration > 5000 | order by timestamp desc
```

#### API Failure Investigation
```bash
# External API failures
traces | where message contains "HTTP request to" and severityLevel >= 2

# Fallback usage patterns
traces | where message contains "Primary API endpoint failed"
```

#### Performance Analysis
```bash
# Long-running operations
traces | where customDimensions.OperationName != "" and customDimensions.Duration > 3000
```

## 📈 Best Practices

### Do's
✅ Use structured logging with consistent property names  
✅ Include correlation IDs in all log entries  
✅ Log user actions for audit trails  
✅ Monitor external dependencies  
✅ Use appropriate log levels  
✅ Include context in error messages  

### Don'ts
❌ Log sensitive information (passwords, API keys)  
❌ Log excessive detail in production  
❌ Use string concatenation in log messages  
❌ Ignore exceptions without logging  
❌ Log in tight loops without throttling  

## 📝 Configuration

### Environment Variables
- `ApplicationInsights:ConnectionString`: Application Insights connection string
- `Logging:LogLevel:*`: Override log levels per category

### Startup Configuration
The logging is configured in `Program.cs` with:
- Console provider for development
- Application Insights provider for production
- Custom request logging middleware
- Structured logging configuration

This comprehensive logging implementation provides full observability into the AiStockTradeApp UI application, enabling effective monitoring, debugging, and performance optimization.
