# AiStockTradeApp.Entities - Domain Models & View Models

## 🚀 Project Overview

A .NET class library containing **domain models** (entities) and **view models** used throughout the AI Stock Trade application solution. This project serves as the central data contracts library, providing strongly-typed models for stock data, user management, and UI presentation.

## 🏗️ Architecture Role

This project serves as the **data contracts foundation** for the entire solution:

- **Domain Entities** - Core business models representing database entities
- **View Models** - UI-specific data transfer objects for MVC views
- **Data Transfer Objects** - API communication models
- **Shared Models** - Common data structures across projects
- **Validation Attributes** - Input validation and business rules

### Key Responsibilities

- **Define data structure** for stock information and market data
- **User management models** with ASP.NET Identity integration
- **Chart and visualization models** for frontend components
- **API response models** for external service integration
- **Validation rules** and business constraints
- **Database relationships** through navigation properties

## 📁 Project Structure

```
AiStockTradeApp.Entities/
├── Models/                         # Domain Entities (Database Models)
│   ├── ApplicationUser.cs          # User identity and authentication entity
│   ├── HistoricalPrice.cs          # Historical price data with technical indicators
│   ├── ListedStock.cs              # Listed company metadata and stock information
│   ├── PriceAlert.cs               # User-defined price alert notifications
│   ├── StockData.cs                # Primary stock data entity with real-time info
│   ├── UserPorfolioItem.cs         # User portfolio holdings and positions
│   ├── UserPreferences.cs          # User settings and localization preferences
│   ├── UserWatchlistItem.cs        # User-specific persistent watchlist entries
│   └── WatchlistItem.cs            # Session-based watchlist for anonymous users
├── ViewModels/                     # UI Data Transfer Objects
│   ├── ChartDataPoint.cs           # Chart visualization data points
│   ├── DashboardViewModel.cs       # Main dashboard display model
│   ├── ErrorViewModel.cs           # Error page display model
│   ├── HistoricalDataViewModel.cs  # Historical data visualization model
│   ├── LoginViewModel.cs           # User authentication form model
│   ├── PortfolioViewModel.cs       # Portfolio management display model
│   ├── RegisterViewModel.cs        # User registration form model
│   ├── StockAnalysisViewModel.cs   # AI analysis display and interaction model
│   ├── StockSearchViewModel.cs     # Stock search results and filtering
│   ├── StockViewModel.cs           # Stock display and interaction model
│   ├── UserProfileViewModel.cs     # User profile management model
│   └── WatchlistViewModel.cs       # Watchlist display and management model
└── AiStockTradeApp.Entities.csproj # Project configuration
```

## 🔧 Technology Stack

### Core Framework
- **.NET 9** - Latest LTS framework with modern C# features
- **System.ComponentModel.DataAnnotations** - Validation attributes
- **Microsoft.AspNetCore.Identity** - User authentication models

### Data Validation
- **Built-in Validation Attributes** - Required, Range, StringLength
- **Custom Validation** - Business rule validation
- **Regular Expressions** - Format validation for symbols and inputs
- **Display Attributes** - UI metadata and localization keys

## 📊 Domain Models (Entities)

### Core Business Entities

#### StockData
Primary entity representing current stock information with real-time market data:

```csharp
public class StockData
{
    [Key]
    [Required]
    [StringLength(10)]
    public string Symbol { get; set; }          // Primary key (e.g., "AAPL")
    
    [Required]
    [StringLength(200)]
    public string CompanyName { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal CurrentPrice { get; set; }
    
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal PreviousClose { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal DayHigh { get; set; }
    public decimal DayLow { get; set; }
    
    [Range(0, long.MaxValue)]
    public long Volume { get; set; }
    
    public long AverageVolume { get; set; }
    public decimal? MarketCap { get; set; }
    public decimal? PeRatio { get; set; }
    
    [StringLength(2000)]
    public string? AIAnalysis { get; set; }
    
    public DateTime LastUpdated { get; set; }
    
    [Required]
    [StringLength(50)]
    public string DataSource { get; set; } = "Unknown";
}
```

#### HistoricalPrice
Time-series data for historical stock prices with technical analysis support:

```csharp
public class HistoricalPrice
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(10)]
    public string Symbol { get; set; }
    
    public DateTime Date { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal Open { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal High { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal Low { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal Close { get; set; }
    
    public decimal AdjustedClose { get; set; }
    
    [Range(0, long.MaxValue)]
    public long Volume { get; set; }
    
    // Navigation property
    public virtual StockData? Stock { get; set; }
}
```

#### ListedStock
Metadata about listed companies and their stock information:

```csharp
public class ListedStock
{
    [Key]
    [Required]
    [StringLength(10)]
    public string Symbol { get; set; }
    
    [Required]
    [StringLength(200)]
    public string CompanyName { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Exchange { get; set; }
    
    [StringLength(100)]
    public string? Sector { get; set; }
    
    [StringLength(100)]
    public string? Industry { get; set; }
    
    [StringLength(3)]
    public string? Currency { get; set; } = "USD";
    
    public bool IsActive { get; set; } = true;
    public DateTime LastUpdated { get; set; }
}
```

#### ApplicationUser
Extended user entity with application-specific properties:

```csharp
public class ApplicationUser : IdentityUser
{
    [StringLength(50)]
    public string? FirstName { get; set; }
    
    [StringLength(50)]
    public string? LastName { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    [StringLength(10)]
    public string PreferredCulture { get; set; } = "en";
    
    public bool EnablePriceAlerts { get; set; } = true;
    public bool EnableEmailNotifications { get; set; } = true;
    
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}".Trim();
    
    // Navigation properties
    public virtual ICollection<UserWatchlistItem> WatchlistItems { get; set; } = new List<UserWatchlistItem>();
    public virtual ICollection<UserPortfolioItem> PortfolioItems { get; set; } = new List<UserPortfolioItem>();
    public virtual ICollection<PriceAlert> PriceAlerts { get; set; } = new List<PriceAlert>();
}
```

#### UserWatchlistItem
Persistent watchlist entries for authenticated users:

```csharp
public class UserWatchlistItem
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; }
    
    [Required]
    [StringLength(10)]
    public string Symbol { get; set; }
    
    [StringLength(100)]
    public string? UserAlias { get; set; }
    
    public DateTime AddedAt { get; set; }
    public int SortOrder { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser User { get; set; }
    public virtual StockData? Stock { get; set; }
}
```

#### UserPortfolioItem
User portfolio holdings and investment positions:

```csharp
public class UserPortfolioItem
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; }
    
    [Required]
    [StringLength(10)]
    public string Symbol { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal Quantity { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal PurchasePrice { get; set; }
    
    public DateTime PurchaseDate { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser User { get; set; }
    public virtual StockData? Stock { get; set; }
    
    // Calculated properties
    [NotMapped]
    public decimal TotalValue => Quantity * PurchasePrice;
}
```

#### PriceAlert
User-defined price alerts for stock monitoring:

```csharp
public class PriceAlert
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; }
    
    [Required]
    [StringLength(10)]
    public string Symbol { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal TargetPrice { get; set; }
    
    public AlertType AlertType { get; set; } // Above, Below, Change
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? TriggeredAt { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser User { get; set; }
    public virtual StockData? Stock { get; set; }
}

public enum AlertType
{
    Above,
    Below,
    PercentChange
}
```

#### WatchlistItem
Session-based watchlist for anonymous users:

```csharp
public class WatchlistItem
{
    [Required]
    [StringLength(50)]
    public string SessionId { get; set; }
    
    [Required]
    [StringLength(10)]
    public string Symbol { get; set; }
    
    public DateTime AddedAt { get; set; }
    
    // Navigation property
    public virtual StockData? Stock { get; set; }
}
```

## 🎨 View Models (UI Models)

### Dashboard & Main UI

#### DashboardViewModel
Main dashboard display model with comprehensive market overview:

```csharp
public class DashboardViewModel
{
    public List<StockViewModel> WatchlistStocks { get; set; } = new();
    public List<StockViewModel> TopGainers { get; set; } = new();
    public List<StockViewModel> TopLosers { get; set; } = new();
    public List<ChartDataPoint> MarketTrendData { get; set; } = new();
    public MarketSummary MarketSummary { get; set; } = new();
    public string? UserName { get; set; }
    public bool IsAuthenticated { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class MarketSummary
{
    public decimal TotalMarketValue { get; set; }
    public decimal TotalChange { get; set; }
    public decimal TotalChangePercent { get; set; }
    public int TotalStocks { get; set; }
    public int GainersCount { get; set; }
    public int LosersCount { get; set; }
}
```

#### StockViewModel
Individual stock display model with real-time data and UI metadata:

```csharp
public class StockViewModel
{
    [Required]
    [Display(Name = "Symbol")]
    [StringLength(10, MinimumLength = 1)]
    public string Symbol { get; set; }
    
    [Display(Name = "Company Name")]
    public string CompanyName { get; set; }
    
    [Display(Name = "Current Price")]
    [DisplayFormat(DataFormatString = "{0:C}")]
    public decimal CurrentPrice { get; set; }
    
    [Display(Name = "Change")]
    [DisplayFormat(DataFormatString = "{0:C}")]
    public decimal Change { get; set; }
    
    [Display(Name = "Change %")]
    [DisplayFormat(DataFormatString = "{0:P2}")]
    public decimal ChangePercent { get; set; }
    
    [Display(Name = "Previous Close")]
    [DisplayFormat(DataFormatString = "{0:C}")]
    public decimal PreviousClose { get; set; }
    
    [Display(Name = "Volume")]
    [DisplayFormat(DataFormatString = "{0:N0}")]
    public long Volume { get; set; }
    
    [Display(Name = "Market Cap")]
    [DisplayFormat(DataFormatString = "{0:C0}")]
    public decimal? MarketCap { get; set; }
    
    [Display(Name = "P/E Ratio")]
    [DisplayFormat(DataFormatString = "{0:F2}")]
    public decimal? PeRatio { get; set; }
    
    [Display(Name = "AI Analysis")]
    public string? AIAnalysis { get; set; }
    
    [Display(Name = "Last Updated")]
    [DisplayFormat(DataFormatString = "{0:HH:mm:ss}")]
    public DateTime LastUpdated { get; set; }
    
    [Display(Name = "Data Source")]
    public string DataSource { get; set; }
    
    // UI Helper Properties
    public string ChangeClass => Change >= 0 ? "text-success" : "text-danger";
    public string ChangeIcon => Change >= 0 ? "fa-arrow-up" : "fa-arrow-down";
    public string FormattedMarketCap => FormatMarketCap(MarketCap);
    
    private static string FormatMarketCap(decimal? marketCap)
    {
        if (marketCap == null) return "N/A";
        
        return marketCap switch
        {
            >= 1_000_000_000_000 => $"{marketCap / 1_000_000_000_000:F2}T",
            >= 1_000_000_000 => $"{marketCap / 1_000_000_000:F2}B",
            >= 1_000_000 => $"{marketCap / 1_000_000:F2}M",
            _ => $"{marketCap:C0}"
        };
    }
}
```

#### HistoricalDataViewModel
Historical data visualization model for charts and analysis:

```csharp
public class HistoricalDataViewModel
{
    [Required]
    public string Symbol { get; set; }
    
    public string CompanyName { get; set; }
    
    public List<ChartDataPoint> PriceData { get; set; } = new();
    public List<ChartDataPoint> VolumeData { get; set; } = new();
    
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    public decimal HighestPrice { get; set; }
    public decimal LowestPrice { get; set; }
    public decimal AveragePrice { get; set; }
    public long AverageVolume { get; set; }
    
    public decimal TotalReturn { get; set; }
    public decimal AnnualizedReturn { get; set; }
    public decimal Volatility { get; set; }
    
    public string ChartTitle => $"{CompanyName} ({Symbol}) - Historical Data";
}
```

### Chart & Visualization Models

#### ChartDataPoint
Generic chart data point for various visualization needs:

```csharp
public class ChartDataPoint
{
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
    public decimal? SecondaryValue { get; set; }
    public string? Label { get; set; }
    public string? Category { get; set; }
    
    // Chart.js compatible properties
    public string FormattedDate => Date.ToString("yyyy-MM-dd");
    public string FormattedValue => Value.ToString("F2", CultureInfo.InvariantCulture);
    public string FormattedSecondaryValue => SecondaryValue?.ToString("F2", CultureInfo.InvariantCulture) ?? "0";
}
```

### Authentication & User Management

#### LoginViewModel
User authentication form model:

```csharp
public class LoginViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; }
    
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; }
    
    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
    
    public string? ReturnUrl { get; set; }
}
```

#### RegisterViewModel
User registration form model with validation:

```csharp
public class RegisterViewModel
{
    [Required]
    [StringLength(50)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; }
    
    [Required]
    [StringLength(50)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; }
    
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; }
    
    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; }
    
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; }
    
    [Display(Name = "Preferred Language")]
    public string PreferredCulture { get; set; } = "en";
}
```

#### UserProfileViewModel
User profile management and preferences:

```csharp
public class UserProfileViewModel
{
    public string UserId { get; set; }
    
    [Required]
    [StringLength(50)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; }
    
    [Required]
    [StringLength(50)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; }
    
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; }
    
    [Display(Name = "Preferred Language")]
    public string PreferredCulture { get; set; }
    
    [Display(Name = "Enable Price Alerts")]
    public bool EnablePriceAlerts { get; set; }
    
    [Display(Name = "Enable Email Notifications")]
    public bool EnableEmailNotifications { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    public int WatchlistCount { get; set; }
    public int PortfolioCount { get; set; }
    public int AlertsCount { get; set; }
}
```

### Portfolio & Watchlist Management

#### PortfolioViewModel
Portfolio management and performance tracking:

```csharp
public class PortfolioViewModel
{
    public List<PortfolioItemViewModel> Holdings { get; set; } = new();
    public PortfolioSummary Summary { get; set; } = new();
    public List<ChartDataPoint> PerformanceChart { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

public class PortfolioItemViewModel
{
    public int Id { get; set; }
    public string Symbol { get; set; }
    public string CompanyName { get; set; }
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string? Notes { get; set; }
    
    // Calculated fields
    public decimal TotalCost => Quantity * PurchasePrice;
    public decimal CurrentValue => Quantity * CurrentPrice;
    public decimal GainLoss => CurrentValue - TotalCost;
    public decimal GainLossPercent => TotalCost > 0 ? (GainLoss / TotalCost) * 100 : 0;
    
    public string GainLossClass => GainLoss >= 0 ? "text-success" : "text-danger";
}

public class PortfolioSummary
{
    public decimal TotalValue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalGainLoss { get; set; }
    public decimal TotalGainLossPercent { get; set; }
    public int TotalHoldings { get; set; }
    public decimal DayChange { get; set; }
    public decimal DayChangePercent { get; set; }
}
```

#### WatchlistViewModel
Watchlist management for both anonymous and authenticated users:

```csharp
public class WatchlistViewModel
{
    public List<StockViewModel> Stocks { get; set; } = new();
    public bool IsAuthenticated { get; set; }
    public int TotalItems { get; set; }
    public DateTime LastUpdated { get; set; }
    
    public string AddStockUrl { get; set; } = "/Stock/AddToWatchlist";
    public string RemoveStockUrl { get; set; } = "/Stock/RemoveFromWatchlist";
    
    // Summary statistics
    public int GainersCount => Stocks.Count(s => s.Change > 0);
    public int LosersCount => Stocks.Count(s => s.Change < 0);
    public decimal AverageChange => Stocks.Any() ? Stocks.Average(s => s.ChangePercent) : 0;
}
```

### Search & Analysis

#### StockSearchViewModel
Stock search functionality with filtering and results:

```csharp
public class StockSearchViewModel
{
    [Display(Name = "Search Term")]
    [StringLength(100)]
    public string? SearchTerm { get; set; }
    
    [Display(Name = "Sector")]
    public string? Sector { get; set; }
    
    [Display(Name = "Exchange")]
    public string? Exchange { get; set; }
    
    [Display(Name = "Minimum Price")]
    [Range(0, double.MaxValue)]
    public decimal? MinPrice { get; set; }
    
    [Display(Name = "Maximum Price")]
    [Range(0, double.MaxValue)]
    public decimal? MaxPrice { get; set; }
    
    public List<StockViewModel> Results { get; set; } = new();
    public List<string> AvailableSectors { get; set; } = new();
    public List<string> AvailableExchanges { get; set; } = new();
    
    public int TotalResults { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages => (int)Math.Ceiling((double)TotalResults / PageSize);
    
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
```

#### StockAnalysisViewModel
AI analysis display and interaction model:

```csharp
public class StockAnalysisViewModel
{
    [Required]
    public string Symbol { get; set; }
    
    public string CompanyName { get; set; }
    public StockViewModel CurrentData { get; set; }
    
    [Display(Name = "AI Analysis")]
    public string? AIAnalysis { get; set; }
    
    public List<AnalysisPoint> KeyPoints { get; set; } = new();
    public AnalysisRating OverallRating { get; set; }
    public DateTime AnalysisDate { get; set; }
    
    public List<ChartDataPoint> TechnicalIndicators { get; set; } = new();
    public List<NewsItem> RelatedNews { get; set; } = new();
    
    public bool CanRequestNewAnalysis => 
        AnalysisDate < DateTime.UtcNow.AddHours(-1);
}

public class AnalysisPoint
{
    public string Category { get; set; }
    public string Description { get; set; }
    public AnalysisRating Impact { get; set; }
    public string? Details { get; set; }
}

public class NewsItem
{
    public string Title { get; set; }
    public string Summary { get; set; }
    public string Source { get; set; }
    public DateTime PublishedAt { get; set; }
    public string? Url { get; set; }
}

public enum AnalysisRating
{
    StrongSell = 1,
    Sell = 2,
    Hold = 3,
    Buy = 4,
    StrongBuy = 5
}
```

### Error Handling

#### ErrorViewModel
Error page display model with tracking information:

```csharp
public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorDetails { get; set; }
    public int StatusCode { get; set; }
    public DateTime Timestamp { get; set; }
    
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    public bool ShowDetails => !string.IsNullOrEmpty(ErrorDetails);
    
    public string FriendlyMessage => StatusCode switch
    {
        404 => "The page you requested could not be found.",
        500 => "An internal server error occurred.",
        403 => "You don't have permission to access this resource.",
        _ => "An unexpected error occurred."
    };
}
```

## 🔧 Dependencies

### NuGet Packages

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="9.0.0" />
    <PackageReference Include="System.ComponentModel.Annotations" Version="6.0.0" />
  </ItemGroup>
</Project>
```

### Referenced By

This project is referenced by:

- **AiStockTradeApp** (Web UI) - View models and display logic
- **AiStockTradeApp.Api** (REST API) - Domain entities and DTOs
- **AiStockTradeApp.DataAccess** (Data Layer) - Entity Framework models
- **AiStockTradeApp.Services** (Business Logic) - Domain models and DTOs
- **AiStockTradeApp.Tests** (Unit Tests) - Test data models
- **AiStockTradeApp.PlaywrightUITests** (UI Tests) - Page object models

## 🎯 Design Patterns & Best Practices

### Model Design Principles

1. **Single Responsibility** - Each model has a clear, focused purpose
2. **Data Annotations** - Comprehensive validation and display attributes
3. **Navigation Properties** - Clear entity relationships
4. **Calculated Properties** - UI-specific computed values
5. **Immutable Data** - Where appropriate, use readonly properties
6. **Culture Awareness** - Proper formatting for international users

### Validation Strategy

```csharp
// Example: Comprehensive validation attributes
public class StockViewModel
{
    [Required(ErrorMessage = "Stock symbol is required")]
    [StringLength(10, MinimumLength = 1, ErrorMessage = "Symbol must be 1-10 characters")]
    [RegularExpression(@"^[A-Z]+$", ErrorMessage = "Symbol must contain only uppercase letters")]
    [Display(Name = "Stock Symbol", Description = "Enter the stock ticker symbol")]
    public string Symbol { get; set; }
    
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    [DisplayFormat(DataFormatString = "{0:C}", ApplyFormatInEditMode = true)]
    public decimal CurrentPrice { get; set; }
}
```

### Localization Support

```csharp
// Display attributes with resource keys for localization
public class StockViewModel
{
    [Display(Name = "StockSymbol", ResourceType = typeof(SharedResource))]
    public string Symbol { get; set; }
    
    [Display(Name = "CurrentPrice", ResourceType = typeof(SharedResource))]
    [DisplayFormat(DataFormatString = "{0:C}")]
    public decimal CurrentPrice { get; set; }
}
```

## 🧪 Testing Support

### Model Testing Patterns

```csharp
// Example test for validation attributes
[Fact]
public void StockViewModel_Symbol_RequiredValidation()
{
    // Arrange
    var model = new StockViewModel { Symbol = "" };
    var validationContext = new ValidationContext(model);
    var validationResults = new List<ValidationResult>();
    
    // Act
    var isValid = Validator.TryValidateObject(model, validationContext, validationResults, true);
    
    // Assert
    Assert.False(isValid);
    Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(StockViewModel.Symbol)));
}
```

### Test Data Builders

```csharp
// Example test data builder
public class StockViewModelBuilder
{
    private StockViewModel _model = new StockViewModel();
    
    public StockViewModelBuilder WithSymbol(string symbol)
    {
        _model.Symbol = symbol;
        return this;
    }
    
    public StockViewModelBuilder WithPrice(decimal price)
    {
        _model.CurrentPrice = price;
        return this;
    }
    
    public StockViewModel Build() => _model;
}
```

## 🔄 Integration Patterns

### Entity Framework Integration

```csharp
// Example DbContext usage with entities
public class StockDataContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<StockData> StockData { get; set; }
    public DbSet<HistoricalPrice> HistoricalPrices { get; set; }
    public DbSet<ListedStock> ListedStocks { get; set; }
    public DbSet<UserWatchlistItem> UserWatchlistItems { get; set; }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // Configure entity relationships and constraints
        builder.Entity<StockData>()
            .HasKey(s => s.Symbol);
            
        builder.Entity<HistoricalPrice>()
            .HasIndex(h => new { h.Symbol, h.Date })
            .IsUnique();
    }
}
```

### API Controller Integration

```csharp
// Example controller using view models
[ApiController]
[Route("api/[controller]")]
public class StocksController : ControllerBase
{
    [HttpGet("{symbol}")]
    public async Task<ActionResult<StockViewModel>> GetStock(string symbol)
    {
        var stockData = await _stockService.GetStockDataAsync(symbol);
        var viewModel = new StockViewModel
        {
            Symbol = stockData.Symbol,
            CurrentPrice = stockData.CurrentPrice,
            // ... map other properties
        };
        
        return Ok(viewModel);
    }
}
```

## 🔧 Development Guidelines

### Adding New Models

1. **Domain Entities** - Place in `Models/` folder with appropriate validation attributes
2. **View Models** - Place in `ViewModels/` folder with UI-specific properties
3. **Navigation Properties** - Use virtual properties for EF Core lazy loading
4. **Validation Rules** - Apply comprehensive data annotations
5. **Display Metadata** - Include Display attributes for UI rendering
6. **Documentation** - Add XML comments for IntelliSense support

### Model Conventions

```csharp
// Example: Complete model with all conventions
/// <summary>
/// Represents a stock data entity with real-time market information
/// </summary>
public class StockData
{
    /// <summary>
    /// Stock ticker symbol (primary key)
    /// </summary>
    [Key]
    [Required(ErrorMessage = "Symbol is required")]
    [StringLength(10, MinimumLength = 1)]
    [Display(Name = "Stock Symbol")]
    public string Symbol { get; set; }
    
    /// <summary>
    /// Current stock price in base currency
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Price must be positive")]
    [DisplayFormat(DataFormatString = "{0:C}")]
    [Display(Name = "Current Price")]
    public decimal CurrentPrice { get; set; }
    
    /// <summary>
    /// Navigation property for historical prices
    /// </summary>
    public virtual ICollection<HistoricalPrice> HistoricalPrices { get; set; } = new List<HistoricalPrice>();
}
```

This comprehensive entity library provides the foundation for all data operations in the AI Stock Trade application, ensuring type safety, validation, and consistent data structures across the entire solution.
