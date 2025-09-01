# AiStockTradeApp.Entities

This .NET class library contains domain models and view models used across the AI Stock Trade application solution.

## Project Structure

```
AiStockTradeApp.Entities/
├── Models/
│   ├── ApplicationUser.cs         # User identity and authentication entity
│   ├── HistoricalPrice.cs         # Historical price data entity
│   ├── ListedStock.cs             # Listed company and stock metadata
│   ├── StockData.cs               # Primary stock data entity
│   └── UserPreferences.cs         # User settings and preferences
├── ViewModels/
│   ├── ChartDataPoint.cs          # Chart visualization data
│   ├── DashboardViewModel.cs      # Dashboard display model
│   ├── ErrorViewModel.cs          # Error page model
│   ├── StockAnalysisViewModel.cs  # AI analysis display model
│   ├── StockSearchViewModel.cs    # Stock search results model
│   └── StockViewModel.cs          # Stock display model
└── AiStockTradeApp.Entities.csproj
```

## Domain Models (Entities)

### Core Business Entities

#### StockData
Primary entity representing current stock information:

```csharp
public class StockData
{
    public string Symbol { get; set; }          // Primary key (e.g., "AAPL")
    public string CompanyName { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal PreviousClose { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal DayHigh { get; set; }
    public decimal DayLow { get; set; }
    public long Volume { get; set; }
    public long AverageVolume { get; set; }
    public decimal? MarketCap { get; set; }
    public decimal? PeRatio { get; set; }
    public string? AIAnalysis { get; set; }
    public DateTime LastUpdated { get; set; }
    public string DataSource { get; set; }
    
    // Navigation properties
    public ICollection<HistoricalPrice> HistoricalPrices { get; set; }
    public ICollection<PriceAlert> PriceAlerts { get; set; }
}
```

#### HistoricalPrice
Historical stock price data for charting and analysis:

```csharp
public class HistoricalPrice
{
    public int Id { get; set; }
    public string Symbol { get; set; }          // Foreign key
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal? AdjustedClose { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public StockData Stock { get; set; }
}
```

#### WatchlistItem
User watchlist tracking:

```csharp
public class WatchlistItem
{
    public int Id { get; set; }
    public string SessionId { get; set; }       // Session-based user identification
    public string Symbol { get; set; }
    public DateTime AddedAt { get; set; }
    
    // Navigation properties
    public StockData Stock { get; set; }
}
```

#### PriceAlert
Price threshold alerts:

```csharp
public class PriceAlert
{
    public int Id { get; set; }
    public string Symbol { get; set; }
    public string SessionId { get; set; }
    public decimal TargetPrice { get; set; }
    public bool IsAbove { get; set; }           // True for above, false for below
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? TriggeredAt { get; set; }
    
    // Navigation properties
    public StockData Stock { get; set; }
}
```

#### CachedStockData
Performance caching entity:

```csharp
public class CachedStockData
{
    public string Symbol { get; set; }          // Primary key
    public string DataJson { get; set; }        // Serialized stock data
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Source { get; set; }          // Data source identifier
}
```

#### User
User authentication and profile:

```csharp
public class User : IdentityUser
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsTestUser { get; set; }        // For development/testing
    
    // Navigation properties
    public ICollection<WatchlistItem> WatchlistItems { get; set; }
    public ICollection<PriceAlert> PriceAlerts { get; set; }
}
```

## View Models (DTOs)

### Display and Transfer Models

#### DashboardViewModel
Main dashboard display model:

```csharp
public class DashboardViewModel
{
    public List<StockViewModel> WatchlistStocks { get; set; }
    public decimal TotalPortfolioValue { get; set; }
    public decimal TotalPortfolioChange { get; set; }
    public decimal TotalPortfolioChangePercent { get; set; }
    public int StockCount { get; set; }
    public DateTime LastUpdated { get; set; }
    public string CurrentUser { get; set; }
    public List<ChartDataPoint> PortfolioChart { get; set; }
}
```

#### StockViewModel
Individual stock display model:

```csharp
public class StockViewModel
{
    public string Symbol { get; set; }
    public string CompanyName { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal PreviousClose { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal DayHigh { get; set; }
    public decimal DayLow { get; set; }
    public long Volume { get; set; }
    public string FormattedVolume { get; set; }
    public decimal? MarketCap { get; set; }
    public string FormattedMarketCap { get; set; }
    public decimal? PeRatio { get; set; }
    public string ChangeDirection { get; set; }  // "up", "down", "flat"
    public string LastUpdated { get; set; }
    public string DataSource { get; set; }
    public bool HasAIAnalysis { get; set; }
    public List<ChartDataPoint> ChartData { get; set; }
}
```

#### StockAnalysisViewModel
AI analysis display model:

```csharp
public class StockAnalysisViewModel
{
    public string Symbol { get; set; }
    public string CompanyName { get; set; }
    public string Analysis { get; set; }
    public string Recommendation { get; set; }   // "Buy", "Hold", "Sell"
    public decimal ConfidenceScore { get; set; }
    public List<string> KeyPoints { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string TrendDirection { get; set; }   // "Bullish", "Bearish", "Neutral"
}
```

#### ChartDataPoint
Chart visualization data:

```csharp
public class ChartDataPoint
{
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
    public string Label { get; set; }
    public string FormattedDate { get; set; }
    public string FormattedValue { get; set; }
}
```

#### StockSearchViewModel
Search results model:

```csharp
public class StockSearchViewModel
{
    public string Symbol { get; set; }
    public string CompanyName { get; set; }
    public string Exchange { get; set; }
    public string Sector { get; set; }
    public string Industry { get; set; }
    public bool IsInWatchlist { get; set; }
    public decimal? CurrentPrice { get; set; }
}
```

#### ErrorViewModel
Error page display model:

```csharp
public class ErrorViewModel
{
    public string RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    public string ErrorMessage { get; set; }
    public string ErrorCode { get; set; }
    public DateTime Timestamp { get; set; }
}
```

## Entity Relationships

### Primary Relationships

- **StockData** (1) → **HistoricalPrice** (Many)
- **StockData** (1) → **PriceAlert** (Many)
- **User** (1) → **WatchlistItem** (Many)
- **User** (1) → **PriceAlert** (Many)
- **StockData** (1) → **WatchlistItem** (Many)

### Database Constraints

- **Unique indexes** on Symbol fields
- **Composite indexes** for performance optimization
- **Foreign key constraints** for data integrity
- **Check constraints** for data validation

## Data Annotations

Entities use data annotations for validation and database mapping:

```csharp
[Required]
[StringLength(10)]
public string Symbol { get; set; }

[Range(0.01, double.MaxValue)]
public decimal CurrentPrice { get; set; }

[DataType(DataType.DateTime)]
public DateTime LastUpdated { get; set; }
```

## Usage Guidelines

### Entity Usage

1. **Use entities** for database operations and business logic
2. **Map to view models** for UI display and API responses
3. **Validate input** using data annotations
4. **Handle nulls** appropriately for optional properties

### View Model Usage

1. **Create specific view models** for different UI contexts
2. **Include only necessary data** to minimize payload
3. **Format data** for display (currency, dates, percentages)
4. **Add computed properties** for UI convenience

### Mapping Examples

```csharp
// Entity to ViewModel mapping
public static StockViewModel ToViewModel(this StockData entity)
{
    return new StockViewModel
    {
        Symbol = entity.Symbol,
        CompanyName = entity.CompanyName,
        CurrentPrice = entity.CurrentPrice,
        Change = entity.Change,
        ChangePercent = entity.ChangePercent,
        ChangeDirection = entity.Change > 0 ? "up" : entity.Change < 0 ? "down" : "flat",
        FormattedVolume = FormatLargeNumber(entity.Volume),
        LastUpdated = entity.LastUpdated.ToString("HH:mm:ss")
    };
}
```

## Validation Rules

### Stock Symbol Validation
- 1-10 characters
- Alphanumeric only
- Uppercase conversion

### Price Validation
- Positive values only
- Maximum precision of 4 decimal places
- Range validation based on data source

### Date Validation
- Valid date ranges
- Business day validation for trading
- Time zone handling

## Dependencies

This project has minimal dependencies:

- **System.ComponentModel.DataAnnotations** - Validation attributes
- **Microsoft.AspNetCore.Identity** - User authentication (for User entity)

## Testing Support

### Test Data Creation

```csharp
public static class TestDataFactory
{
    public static StockData CreateTestStock(string symbol = "TEST", decimal price = 100.00m)
    {
        return new StockData
        {
            Symbol = symbol,
            CompanyName = $"Test Company {symbol}",
            CurrentPrice = price,
            Change = 1.50m,
            ChangePercent = 1.52m,
            LastUpdated = DateTime.UtcNow
        };
    }
}
```

## Related Projects

- **[AiStockTradeApp](../AiStockTradeApp/)** - Web UI that uses these models
- **[AiStockTradeApp.Api](../AiStockTradeApp.Api/)** - API that returns these models
- **[AiStockTradeApp.DataAccess](../AiStockTradeApp.DataAccess/)** - Data access using these entities
- **[AiStockTradeApp.Services](../AiStockTradeApp.Services/)** - Services that manipulate these models
