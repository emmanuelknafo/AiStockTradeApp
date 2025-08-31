# API Logging Enhancements

This document describes the comprehensive logging enhancements made to the AI Stock Trade API project.

## Overview

The API project now includes enhanced logging capabilities to improve observability, debugging, and monitoring of the application. The enhancements include structured logging, request/response logging, error tracking, and performance monitoring.

## Logging Components

### 1. Enhanced Configuration

**Location**: `appsettings.json`, `appsettings.Development.json`

The logging configuration has been enhanced with:
- Detailed log level configuration for different namespaces
- Console logging with scope inclusion
- Structured logging output
- Different settings for development vs production

#### Production Settings (`appsettings.json`)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.Hosting": "Information",
      "Microsoft.AspNetCore.Routing": "Information",
      "Microsoft.EntityFrameworkCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "AiStockTradeApp": "Debug",
      "AiStockTradeApp.Api": "Debug",
      "AiStockTradeApp.Services": "Debug",
      "AiStockTradeApp.DataAccess": "Information",
      "System.Net.Http.HttpClient": "Information"
    },
    "Console": {
      "IncludeScopes": true,
      "LogToStandardErrorThreshold": "Warning"
    }
  }
}
```

#### Development Settings (`appsettings.Development.json`)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.AspNetCore.Hosting": "Information",
      "Microsoft.AspNetCore.Routing": "Debug",
      "Microsoft.EntityFrameworkCore": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Debug",
      "AiStockTradeApp": "Trace",
      "AiStockTradeApp.Api": "Trace",
      "AiStockTradeApp.Services": "Trace",
      "AiStockTradeApp.DataAccess": "Debug",
      "System.Net.Http.HttpClient": "Debug"
    },
    "Console": {
      "IncludeScopes": true,
      "LogToStandardErrorThreshold": "Warning",
      "TimestampFormat": "yyyy-MM-dd HH:mm:ss.fff "
    }
  }
}
```

### 2. Request/Response Logging Middleware

**Location**: `Middleware/RequestResponseLoggingMiddleware.cs`

This middleware provides comprehensive HTTP request and response logging with:

#### Features
- **Request Logging**: Method, path, headers, body (configurable), user agent, IP address
- **Response Logging**: Status code, headers, body (configurable), timing
- **Correlation IDs**: Automatic generation and tracking of request correlation IDs
- **Performance Monitoring**: Detection and logging of slow requests
- **Security**: Automatic redaction of sensitive headers (Authorization, Cookie, etc.)
- **Configurable Body Logging**: Option to log request/response bodies with size limits

#### Configuration Options
```json
{
  "RequestResponseLogging": {
    "LogRequestBody": false,      // Security consideration for production
    "LogResponseBody": false,     // Performance consideration for production  
    "MaxBodySizeToLog": 4096,     // Maximum body size in bytes
    "SlowRequestThresholdMs": 2000 // Threshold for slow request warnings
  }
}
```

#### Development vs Production Settings
- **Development**: Body logging enabled with higher size limits and lower slow request threshold
- **Production**: Body logging disabled for security and performance

### 3. Global Exception Handling Middleware

**Location**: `Middleware/GlobalExceptionHandlingMiddleware.cs`

Provides centralized exception handling with:

#### Features
- **Structured Error Responses**: RFC 7807 compliant problem details format
- **Comprehensive Logging**: Full exception details with context
- **Application Insights Integration**: Automatic exception tracking
- **Security**: Stack traces only in development environment
- **Correlation Tracking**: Links exceptions to specific requests
- **HTTP Status Mapping**: Intelligent mapping of exceptions to appropriate HTTP status codes

#### Error Response Format
```json
{
  "correlationId": "abc12345",
  "type": "validation_error", 
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid request parameters.",
  "path": "/api/stocks/quote",
  "timestamp": "2025-01-15T10:30:00Z",
  "developerMessage": "Full stack trace (development only)"
}
```

### 4. API Logging Extensions

**Location**: `Middleware/ApiLoggingExtensions.cs`

Provides structured logging methods for common API operations:

#### Available Methods
- `LogApiOperationAsync<T>()`: Logs API endpoint execution with timing
- `LogExternalApiCallAsync<T>()`: Logs external API calls with performance metrics
- `LogDatabaseOperationAsync<T>()`: Logs database operations with timing
- `LogValidationError()`: Logs validation errors with context
- `LogStockDataEvent()`: Logs business events specific to stock operations
- `LogImportJobProgress()`: Logs import job status and progress

#### Usage Example
```csharp
return await logger.LogApiOperationAsync(
    "GetStockQuote",
    async () =>
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            logger.LogValidationError("GetStockQuote", "symbol", symbol, "Symbol is required", correlationId);
            return Results.BadRequest(new { error = "Symbol is required" });
        }
        
        var result = await svc.GetStockQuoteAsync(symbol);
        logger.LogStockDataEvent("QuoteRetrieved", symbol, new { price = result.Data?.Price }, correlationId);
        return Results.Ok(result.Data);
    },
    new { symbol },
    correlationId);
```

### 5. Enhanced Background Service Logging

**Location**: `Background/ImportJobProcessor.cs`

The import job processor now includes comprehensive logging:

#### Features
- **Job Lifecycle Logging**: Start, progress, completion, and failure logging
- **Progress Tracking**: Real-time progress updates with detailed metrics
- **Error Details**: Comprehensive error logging with context
- **Performance Monitoring**: Batch processing metrics and timing
- **Structured Context**: Scoped logging with job IDs and metadata

#### Log Examples
```
[INFO] Import job processor started - ProcessorId: MACHINE01
[INFO] Dequeued import job abc123 of type ListedStocksCsv
[INFO] Starting import job abc123 processing - Type: ListedStocksCsv, Source: stocks.csv
[INFO] Processing 10000 listed stock records
[DEBUG] Processed 500/10000 listed stock records
[INFO] Completed processing 9856 listed stock records
[INFO] Completed import job abc123 successfully
```

## Correlation ID Tracking

All middleware and logging components support correlation ID tracking:

1. **Automatic Generation**: If no correlation ID is provided, one is generated
2. **Header Propagation**: Correlation IDs are added to response headers
3. **Structured Logging**: All log entries include correlation IDs in scope
4. **Request Tracing**: End-to-end tracking of requests through the system

## Performance Monitoring

The logging system includes several performance monitoring features:

### Request Performance
- Automatic detection of slow requests (configurable threshold)
- Request duration logging
- Performance warnings for operations exceeding thresholds

### Database Performance  
- Database operation timing
- Slow query detection and warnings
- Entity Framework command logging (development)

### External API Performance
- External API call timing
- Timeout and failure tracking
- Performance metrics for third-party integrations

## Security Considerations

### Sensitive Data Protection
- Automatic redaction of sensitive headers (Authorization, Cookie, API keys)
- Configurable body logging (disabled in production)
- Stack trace exposure only in development environment

### Log Level Management
- Production logs focus on errors and important events
- Development logs include debug and trace information
- Sensitive operations use appropriate log levels

## Application Insights Integration

The enhanced logging integrates with Azure Application Insights:

### Features
- Automatic exception tracking with telemetry
- Custom metrics and events
- Performance counter collection
- Request and dependency tracking

### Configuration
Application Insights is configured via connection string in appsettings:
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=...;IngestionEndpoint=...;"
  }
}
```

## Troubleshooting

### Common Scenarios

#### High Log Volume
If logs are too verbose:
1. Adjust log levels in appsettings.json
2. Disable body logging in production
3. Increase slow request thresholds

#### Missing Correlation IDs
- Ensure `RequestResponseLoggingMiddleware` is registered early in pipeline
- Check that `X-Correlation-ID` header is being set

#### Performance Impact
- Disable body logging for high-traffic endpoints
- Adjust log levels to reduce verbosity
- Consider async logging for high-volume scenarios

### Log Analysis

#### Searching Logs
Use correlation IDs to trace requests:
```
CorrelationId="abc12345"
```

Filter by operation:
```
OperationName="GetStockQuote"
```

Find performance issues:
```
Duration > 2000
```

#### Key Metrics to Monitor
- Request duration trends
- Error rates by endpoint
- External API performance
- Database operation timing
- Import job success rates

## Best Practices

### For Developers
1. **Use Structured Logging**: Always use structured parameters in log messages
2. **Include Context**: Add relevant business context to log entries
3. **Appropriate Log Levels**: Use correct log levels for different scenarios
4. **Correlation IDs**: Always pass correlation IDs through operation chains
5. **Performance Awareness**: Log timing for important operations

### For Operations
1. **Monitor Error Rates**: Set up alerts for error rate increases
2. **Performance Thresholds**: Monitor and alert on slow operations
3. **Log Retention**: Configure appropriate log retention policies
4. **Sensitive Data**: Ensure no sensitive data in logs in production

### For Troubleshooting
1. **Start with Correlation ID**: Use correlation IDs to trace specific requests
2. **Check Timing**: Look for performance bottlenecks in operation timing
3. **Error Context**: Use structured error information for root cause analysis
4. **Import Jobs**: Monitor import job status and progress for data operations

## Future Enhancements

Potential future improvements to the logging system:

1. **Distributed Tracing**: OpenTelemetry integration for microservices
2. **Log Aggregation**: ELK stack or similar for centralized log management
3. **Metrics Dashboard**: Custom dashboards for business metrics
4. **Automated Alerting**: Proactive alerting based on log patterns
5. **Log Sampling**: Intelligent sampling for high-volume scenarios
