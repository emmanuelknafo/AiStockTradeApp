# AiStockTradeApp.Services

This .NET class library contains service implementations, background services, and interfaces for business logic used by the AI Stock Trade application.

## Project Structure

```
AiStockTradeApp.Services/
├── Interfaces/
│   ├── IAIAnalysisService.cs          # AI analysis service contract
│   ├── IHistoricalPriceService.cs     # Historical data service contract
│   ├── IListedStockService.cs         # Listed stocks service contract
│   ├── IStockDataService.cs           # Stock data service contract
│   ├── ITestUserSeedingService.cs     # Test user seeding contract
│   └── IWatchlistService.cs           # Watchlist management contract
├── Implementations/
│   ├── AIAnalysisService.cs           # AI-powered stock analysis
│   ├── ApiStockDataServiceClient.cs   # HTTP client for API communication
│   ├── AuthenticationDiagnosticsService.cs # Authentication diagnostics
│   ├── HistoricalDataFetcher.cs       # External historical data fetching
│   ├── HistoricalPriceService.cs      # Historical price management
│   ├── ListedStockService.cs          # Listed stocks data management
│   ├── StockDataService.cs            # Primary stock data service
│   ├── TestUserSeedingService.cs      # Test user data seeding
│   └── WatchlistService.cs            # Watchlist management implementation
├── BackgroundServices/
│   └── (Background workers for timed refresh, cache maintenance, etc.)
└── LoggingExtensions.cs               # Logging utility extensions
```

## Service Overview

### Core Services

#### IStockDataService
Primary interface for stock data operations with multiple implementations:
- **StockDataService** - Direct integration with external APIs (Alpha Vantage, Yahoo Finance, Twelve Data)
- **ApiStockDataServiceClient** - HTTP client for communicating with AiStockTradeApp.Api

#### IAIAnalysisService
Provides AI-powered stock analysis and insights:
- Trend analysis based on price movements
- Investment recommendations (Buy/Hold/Sell)
- Market sentiment analysis
- Performance summaries

#### IWatchlistService
Manages user watchlists and portfolio tracking:
- Session-based watchlist storage
- Add/remove stocks from watchlist
- Portfolio performance calculations
- Watchlist persistence across sessions

#### IHistoricalPriceService
Handles historical stock price data:
- Data retrieval and storage
- Historical chart data preparation
- Price trend analysis
- Data validation and cleansing

#### IListedStockService
Manages publicly listed stock information:
- Stock symbol validation
- Company information retrieval
- Exchange and sector data
- Stock metadata management

### Utility Services

#### AuthenticationDiagnosticsService
Provides authentication and identity diagnostics for troubleshooting.

#### TestUserSeedingService
Seeds test data for development and testing environments.

#### HistoricalDataFetcher
Fetches historical stock data from external sources for offline analysis.

## Service Registration

Services are registered in the application's dependency injection container:

### Web UI Project (AiStockTradeApp)
```csharp
// HTTP client for API communication
builder.Services.AddHttpClient<ApiStockDataServiceClient>((sp, http) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["StockApi:BaseUrl"] ?? "https://localhost:5001";
    http.BaseAddress = new Uri(baseUrl);
});

// Service abstractions pointing to API client
builder.Services.AddScoped<IStockDataService, ApiStockDataServiceClient>();
builder.Services.AddScoped<IAIAnalysisService, AIAnalysisService>();
builder.Services.AddSingleton<IWatchlistService, WatchlistService>();
```

### API Project (AiStockTradeApp.Api)
```csharp
// Direct service implementations with database access
builder.Services.AddScoped<IStockDataService, StockDataService>();
builder.Services.AddScoped<IHistoricalPriceService, HistoricalPriceService>();
builder.Services.AddScoped<IListedStockService, ListedStockService>();
```

## External API Integration

### Supported Stock Data Providers

1. **Alpha Vantage** (Primary)
   - Real-time and historical stock data
   - Requires API key
   - Rate limits: 25 requests/day (free tier)

2. **Yahoo Finance** (Fallback)
   - Free access, no API key required
   - Delayed data (15-20 minutes)
   - Reliable for basic stock information

3. **Twelve Data** (Alternative)
   - Real-time and historical data
   - Optional API key (800 requests/day free)
   - Good coverage for international markets

### Fallback Strategy

The StockDataService implements automatic failover:
1. Attempt Alpha Vantage (if API key configured)
2. Fall back to Yahoo Finance on failure
3. Use Twelve Data as last resort
4. Cache successful responses to minimize API calls

## Configuration

### API Keys Configuration
```json
{
  "AlphaVantage": {
    "ApiKey": "your-alpha-vantage-api-key"
  },
  "TwelveData": {
    "ApiKey": "your-twelve-data-api-key"
  },
  "StockApi": {
    "BaseUrl": "https://localhost:7043"
  }
}
```

### HTTP Client Configuration
```csharp
// Resilient HTTP clients with retry policies
builder.Services.AddHttpClient<AlphaVantageClient>(client =>
{
    client.BaseAddress = new Uri("https://www.alphavantage.co/");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(GetRetryPolicy());
```

## Error Handling

All services implement comprehensive error handling:

- **Graceful degradation** when external APIs fail
- **Detailed logging** for troubleshooting
- **User-friendly error messages** for UI display
- **Retry logic** for transient failures
- **Circuit breaker pattern** for external API protection

## Caching Strategy

Services implement multi-level caching:

- **Memory caching** for frequently accessed data
- **Response caching** for external API calls
- **Session-based caching** for user-specific data
- **Configurable expiration** based on data type

## Testing

Run service tests:

```bash
# All service tests
dotnet test AiStockTradeApp.Tests --filter "Category=Services"

# Specific service tests
dotnet test --filter "ClassName~StockDataServiceTests"
```

## Dependencies

This project references:

- **AiStockTradeApp.Entities** - Domain models and DTOs
- **AiStockTradeApp.DataAccess** - Repository interfaces and data access
- **Microsoft.Extensions.Hosting** - Background services support
- **Microsoft.Extensions.Http** - HTTP client factory
- **Microsoft.Extensions.Caching.Memory** - In-memory caching

## Usage Examples

### Stock Data Retrieval
```csharp
public class StockController : Controller
{
    private readonly IStockDataService _stockDataService;
    
    public async Task<IActionResult> GetStock(string symbol)
    {
        try
        {
            var stock = await _stockDataService.GetStockDataAsync(symbol);
            return Json(stock);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stock data for {Symbol}", symbol);
            return StatusCode(500, "Unable to fetch stock data");
        }
    }
}
```

### Watchlist Management
```csharp
public class WatchlistController : Controller
{
    private readonly IWatchlistService _watchlistService;
    
    public async Task<IActionResult> AddToWatchlist(string symbol)
    {
        var sessionId = HttpContext.Session.Id;
        await _watchlistService.AddStockAsync(sessionId, symbol);
        return Ok();
    }
}
```

## Contributing

When adding new services:

1. **Define interface** in `Interfaces/` folder
2. **Implement service** in `Implementations/` folder
3. **Add comprehensive error handling** and logging
4. **Register in DI container** in both UI and API projects
5. **Add unit tests** in `AiStockTradeApp.Tests/Services/`
6. **Update documentation** as needed

## Related Projects

- **[AiStockTradeApp](../AiStockTradeApp/)** - Web UI that consumes these services
- **[AiStockTradeApp.Api](../AiStockTradeApp.Api/)** - API that implements these services
- **[AiStockTradeApp.Entities](../AiStockTradeApp.Entities/)** - Domain models used by services
- **[AiStockTradeApp.DataAccess](../AiStockTradeApp.DataAccess/)** - Data access layer
