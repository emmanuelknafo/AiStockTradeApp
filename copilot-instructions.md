# AI Stock Tracker Application - Copilot Instructions

## Solution Overview

This is a **multi-project ASP.NET Core solution** implementing an AI-powered stock tracking application with a clean architecture pattern. The solution provides both a web UI and REST API for stock market data analysis with AI-generated insights.

### 🏗️ Architecture Principles
- **Clean Architecture**: Clear separation of concerns across layers
- **Domain-Driven Design**: Business logic isolated in services layer
- **Dependency Injection**: Loosely coupled components
- **API-First Design**: Separate UI and API projects for scalability
- **Testability**: Comprehensive unit and UI testing strategy

## 📂 Project Structure

```
AiStockTradeApp/                    # Root solution folder
├── AiStockTradeApp/                # 🎨 Web UI (MVC) - User interface
├── AiStockTradeApp.Api/            # 🚀 REST API - Backend services
├── AiStockTradeApp.Entities/       # 📊 Data Models - Domain entities
├── AiStockTradeApp.DataAccess/     # 💾 Data Layer - EF Core, repositories
├── AiStockTradeApp.Services/       # 🔧 Business Logic - Core services
├── AiStockTradeApp.Tests/          # ✅ Unit Tests - Service testing
├── AiStockTradeApp.UITests/        # 🤖 UI Tests - Playwright automation
├── AiStockTradeApp.Cli/            # 📟 CLI Tool - Command line utilities
├── infrastructure/                 # ☁️ Azure Infrastructure (Bicep)
├── scripts/                        # 📜 PowerShell automation scripts
└── docker-compose.yml              # 🐳 Local development container setup
```

### 🎯 Project Responsibilities

| Project | Purpose | Key Components | Dependencies |
|---------|---------|----------------|--------------|
| **AiStockTradeApp** | Web UI, MVC controllers, Razor views | Controllers, Views, wwwroot | Services, Entities |
| **AiStockTradeApp.Api** | REST API, background services | API controllers, SignalR hubs | Services, DataAccess |
| **AiStockTradeApp.Entities** | Domain models, view models | Stock data, portfolio models | None (pure models) |
| **AiStockTradeApp.DataAccess** | Database access, EF Core | DbContext, repositories, migrations | Entities |
| **AiStockTradeApp.Services** | Business logic, external APIs | Stock services, AI analysis | DataAccess, Entities |
| **AiStockTradeApp.Tests** | Unit testing | Service tests, controller tests | All projects |
| **AiStockTradeApp.UITests** | End-to-end testing | Playwright page objects | Web UI |
| **AiStockTradeApp.Cli** | Command line tools | Data migration, utilities | Services |

## 🛠️ Technology Stack

### Core Framework
- **.NET 9** - Latest LTS framework
- **ASP.NET Core MVC** - Web UI with Razor views
- **ASP.NET Core Web API** - REST API backend
- **Entity Framework Core 9** - ORM with SQL Server

### Frontend Technologies
- **Razor Views** - Server-side rendering
- **Bootstrap 5** - CSS framework
- **JavaScript/jQuery** - Client-side interactions
- **Chart.js** - Stock data visualization
- **Font Awesome** - Icon library

### Backend Services
- **Dependency Injection** - Built-in DI container
- **SignalR** - Real-time updates
- **Background Services** - Scheduled tasks
- **HTTP Clients** - External API integration
- **Memory Caching** - In-memory performance optimization

### Database & Data
- **SQL Server** - Primary database (Azure SQL/LocalDB)
- **Entity Framework Core** - Code-first migrations
- **In-Memory Database** - Testing scenarios
- **Repository Pattern** - Data access abstraction

### Testing Framework
- **xUnit** - Unit testing framework
- **Moq** - Mocking framework
- **Playwright** - End-to-end browser automation
- **FluentAssertions** - Assertion library

### External Integrations
- **Alpha Vantage API** - Primary stock data source
- **Yahoo Finance API** - Fallback stock data
- **Twelve Data API** - Alternative stock data
- **Azure Application Insights** - Telemetry and monitoring

### DevOps & Infrastructure
- **Azure Container Registry** - Container hosting
- **Azure App Service** - Web application hosting
- **Azure SQL Database** - Managed database
- **Docker** - Containerization
- **Bicep** - Infrastructure as Code
- **GitHub Actions** - CI/CD pipelines

## 🌐 Localization Implementation

The application supports **English (en)** and **French (fr)** localization:

### Resource Management
- **SharedResource.cs** - Marker class in `AiStockTradeApp` namespace
- **SharedResource.resx** - Neutral language resources
- **SharedResource.en.resx** - English translations
- **SharedResource.fr.resx** - French translations

### Localization Pattern
```csharp
// In views - use factory pattern for explicit resource targeting
@inject IStringLocalizerFactory LocFactory
@{
    var localizer = LocFactory.Create("Resources.SharedResource", "AiStockTradeApp");
}
<h1>@localizer["Dashboard_Title"]</h1>
```

### Culture Switching
- **Cookie-based persistence** - Year-long culture storage
- **POST action** - `HomeController.SetLanguage` for culture changes
- **Middleware configuration** - `UseRequestLocalization` with en/fr support

### Localization Guidelines
1. **All user-facing text** must be localized using resource keys
2. **Factory pattern required** - Use `IStringLocalizerFactory.Create()` explicitly
3. **Consistent naming** - Use `ComponentName_ElementName` format for keys
4. **Culture-invariant parsing** - Use `CultureInfo.InvariantCulture` for numeric operations
5. **Resource file alignment** - Ensure .resx files match marker class namespace

## 🔧 Development Patterns

### Dependency Injection Patterns
```csharp
// Service registration in Program.cs
builder.Services.AddScoped<IStockDataService, StockDataService>();
builder.Services.AddSingleton<IWatchlistService, WatchlistService>();
builder.Services.AddHttpClient<ApiStockDataServiceClient>();

// Constructor injection
public class StockController : Controller
{
    private readonly IStockDataService _stockDataService;
    private readonly ILogger<StockController> _logger;
    
    public StockController(IStockDataService stockDataService, ILogger<StockController> logger)
    {
        _stockDataService = stockDataService;
        _logger = logger;
    }
}
```

### Error Handling Strategy
```csharp
// Controller level
public async Task<IActionResult> GetStock(string symbol)
{
    try
    {
        var stock = await _stockDataService.GetStockDataAsync(symbol);
        return Json(stock);
    }
    catch (ArgumentException ex)
    {
        _logger.LogWarning("Invalid stock symbol: {Symbol}", symbol);
        return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error fetching stock data for {Symbol}", symbol);
        return StatusCode(500, "Internal server error");
    }
}
```

### Data Access Patterns
```csharp
// Repository pattern implementation
public class StockDataRepository : IStockDataRepository
{
    private readonly StockDataContext _context;
    
    public async Task<StockData?> GetBySymbolAsync(string symbol)
    {
        return await _context.StockData
            .FirstOrDefaultAsync(s => s.Symbol == symbol);
    }
    
    public async Task SaveAsync(StockData stockData)
    {
        _context.StockData.AddOrUpdate(stockData);
        await _context.SaveChangesAsync();
    }
}
```

## 📊 Database Schema

### Core Entities
- **StockData** - Stock information, prices, analysis
- **WatchlistItem** - User watchlist entries
- **PriceAlert** - User-defined price alerts
- **CachedStockData** - Performance optimization cache

### Entity Relationships
```csharp
public class StockData
{
    public string Symbol { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public string? AIAnalysis { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class WatchlistItem
{
    public string SessionId { get; set; }
    public string Symbol { get; set; }
    public DateTime AddedAt { get; set; }
}
```

### Migration Commands
```bash
# Add new migration
dotnet ef migrations add MigrationName --project AiStockTradeApp.DataAccess

# Update database
dotnet ef database update --project AiStockTradeApp.DataAccess

# Generate SQL script
dotnet ef migrations script --project AiStockTradeApp.DataAccess
```

## 🧪 Testing Strategy

### Unit Testing Guidelines
- **Test all service methods** with different scenarios
- **Mock external dependencies** using Moq
- **Test edge cases** and error conditions
- **Use descriptive test names** following Given_When_Then pattern

```csharp
[Fact]
public async Task GetStockDataAsync_WithValidSymbol_ReturnsStockData()
{
    // Arrange
    var mockRepository = new Mock<IStockDataRepository>();
    var service = new StockDataService(mockRepository.Object);
    
    // Act
    var result = await service.GetStockDataAsync("AAPL");
    
    // Assert
    result.Should().NotBeNull();
    result.Symbol.Should().Be("AAPL");
}
```

### UI Testing with Playwright
- **Page Object Model** - Encapsulate page interactions
- **Test isolation** - Each test should be independent
- **Stable selectors** - Use data attributes for element selection

```csharp
public class StockDashboardPage
{
    private readonly IPage _page;
    
    public async Task AddStockAsync(string symbol)
    {
        await _page.FillAsync("[data-testid='stock-input']", symbol);
        await _page.ClickAsync("[data-testid='add-stock-btn']");
    }
}
```

### Test Execution
```bash
# Run all unit tests
dotnet test AiStockTradeApp.Tests

# Run UI tests
dotnet test AiStockTradeApp.UITests

# Run specific test
dotnet test --filter "TestName"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## 🚀 Deployment & Infrastructure

### Azure Resources (Bicep)
- **App Service Plan** - P0v3 SKU for production workloads
- **Web Apps** - Separate apps for UI and API
- **Azure SQL Database** - Managed database service
- **Container Registry** - Docker image storage
- **Application Insights** - Monitoring and telemetry
- **Key Vault** - Secure configuration storage

### Environment Configuration
```json
// appsettings.{Environment}.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;Trusted_Connection=true;"
  },
  "StockApi": {
    "BaseUrl": "https://api.example.com"
  },
  "AlphaVantage": {
    "ApiKey": "{{vault-secret}}"
  }
}
```

### Docker Configuration
```dockerfile
# Multi-stage build for optimal image size
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
```

## 🛡️ Security Considerations

### Authentication & Authorization
- **Session-based state** - No authentication currently implemented
- **CSRF protection** - Anti-forgery tokens in forms
- **Input validation** - Data annotations and model validation
- **SQL injection prevention** - Entity Framework parameterized queries

### Configuration Security
- **User Secrets** - Local development API keys
- **Azure Key Vault** - Production secret management
- **Environment variables** - Runtime configuration
- **Connection string encryption** - Azure managed encryption

### API Security
- **Rate limiting** - Prevent API abuse
- **CORS configuration** - Cross-origin request control
- **HTTPS enforcement** - TLS encryption required
- **Input sanitization** - XSS prevention

## 🎯 AI & External API Integration

### Stock Data Sources (Fallback Chain)
1. **Alpha Vantage** (Primary) - Requires API key, real-time data
2. **Yahoo Finance** (Fallback) - Free, delayed data
3. **Twelve Data** (Alternative) - API key optional, limited free tier

### AI Analysis Service
```csharp
public async Task<string> GenerateAnalysisAsync(StockData stock)
{
    // Mock AI analysis - replace with actual AI service
    var analysis = $"Stock {stock.Symbol} is trending {(stock.Change > 0 ? "upward" : "downward")}";
    return analysis;
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

## 🔄 Background Services

### Cache Cleanup Service
```csharp
public class CacheCleanupService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupExpiredCacheAsync();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
```

### Stock Price Monitoring
- **Real-time updates** via SignalR
- **Price alert notifications** for threshold breaches
- **Automatic data refresh** at configurable intervals

## 📈 Performance Optimization

### Caching Strategy
- **Memory caching** for frequently accessed stock data
- **Response caching** for static content
- **Database query optimization** with proper indexing
- **Lazy loading** for related entities

### Database Performance
```csharp
// Efficient queries with projections
var stocks = await _context.StockData
    .Where(s => symbols.Contains(s.Symbol))
    .Select(s => new StockViewModel
    {
        Symbol = s.Symbol,
        Price = s.CurrentPrice,
        Change = s.Change
    })
    .ToListAsync();
```

## 🔍 Monitoring & Observability

### Application Insights Integration
- **Request telemetry** - Track HTTP requests and response times
- **Dependency tracking** - Monitor external API calls
- **Exception logging** - Capture and analyze errors
- **Custom metrics** - Business-specific measurements

### Logging Standards
```csharp
// Structured logging with correlation IDs
_logger.LogInformation("Processing stock request for {Symbol} with correlation {CorrelationId}",
    symbol, HttpContext.TraceIdentifier);
```

## 🚨 Troubleshooting Guide

### Common Issues & Solutions

#### 1. Localization Resources Not Loading
**Problem**: Resource keys displayed instead of translated text
**Solution**: 
- Verify `RootNamespace` in .csproj matches marker class namespace
- Confirm .resx files are in correct Resources folder
- Check satellite assemblies in bin/Debug/net9.0/{culture}/ folders

#### 2. Database Connection Issues
**Problem**: Entity Framework connection failures
**Solution**:
- Verify connection string in appsettings.json
- Run `dotnet ef database update` to apply migrations
- Check SQL Server/LocalDB service status

#### 3. External API Failures
**Problem**: Stock data not loading
**Solution**:
- Verify API keys in configuration
- Check rate limits on external services
- Test fallback API sources
- Review HTTP client timeout settings

#### 4. UI Test Failures
**Problem**: Playwright tests failing intermittently
**Solution**:
- Ensure application is running before tests
- Check for database cleanup between tests
- Verify test data isolation
- Update selectors if UI changed

### Debug Configuration
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "AiStockTradeApp": "Debug"
    }
  }
}
```

## 📚 Development Workflow

### Getting Started
1. **Clone repository** and restore packages: `dotnet restore`
2. **Update database** with latest migrations: `dotnet ef database update`
3. **Configure API keys** in User Secrets or appsettings.Development.json
4. **Run application**: `dotnet run --project AiStockTradeApp`

### Making Changes
1. **Create feature branch** from main
2. **Implement changes** following established patterns
3. **Add/update tests** for new functionality
4. **Run full test suite** before committing
5. **Create pull request** with descriptive commit messages

### Code Review Checklist
- [ ] All user-facing text is localized
- [ ] Error handling implemented with appropriate logging
- [ ] Unit tests cover new functionality
- [ ] Database migrations included if schema changed
- [ ] API documentation updated if endpoints changed
- [ ] Security considerations addressed
- [ ] Performance impact assessed

## 🎨 UI/UX Guidelines

### Design System
- **Bootstrap 5** - Consistent component styling
- **Color scheme** - Professional blue/gray theme with accent colors
- **Typography** - Inter font family for readability
- **Icons** - Font Awesome for consistent iconography
- **Responsive design** - Mobile-first approach

### Accessibility
- **ARIA labels** - Screen reader compatibility
- **Keyboard navigation** - Full keyboard accessibility
- **Color contrast** - WCAG 2.1 AA compliance
- **Focus indicators** - Clear focus states for all interactive elements

### Component Standards
```html
<!-- Stock card component -->
<div class="stock-card" data-testid="stock-card-{{symbol}}">
    <div class="stock-header">
        <h3 class="stock-symbol">{{symbol}}</h3>
        <span class="stock-price">{{price}}</span>
    </div>
    <div class="stock-change {{changeClass}}">
        {{change}} ({{changePercent}}%)
    </div>
</div>
```

## 🔧 Maintenance Tasks

### Regular Maintenance
- **Update NuGet packages** monthly for security patches
- **Review and rotate API keys** quarterly
- **Database maintenance** - update statistics, rebuild indexes
- **Monitor application performance** and adjust caching strategies
- **Review error logs** and implement fixes for common issues

### Security Updates
- **Dependency scanning** - Monitor for vulnerable packages
- **Code analysis** - Static analysis with SonarQube or similar
- **Penetration testing** - Regular security assessments
- **Access review** - Audit user permissions and API keys

---

## 🤖 Instructions for AI Assistants

When working with this codebase:

1. **Always preserve the established patterns** - Follow dependency injection, repository patterns, and error handling strategies
2. **Maintain localization compliance** - Ensure all new user-facing text uses the resource file pattern
3. **Test coverage required** - Add appropriate unit tests for new services and UI tests for new features
4. **Database changes** - Create migrations for any schema modifications
5. **Security first** - Consider security implications of all changes, especially input validation and data access
6. **Performance awareness** - Consider caching and query optimization for data-heavy operations
7. **Logging and monitoring** - Add appropriate logging for new functionality to aid troubleshooting
8. **Documentation updates** - Update this file and relevant README files when architectural changes are made

### Code Generation Guidelines
- Use the established namespaces and folder structure
- Follow the dependency injection patterns for service registration
- Implement proper error handling with logging
- Add appropriate validation attributes to models
- Include XML documentation comments for public APIs
- Follow async/await patterns consistently

### Debugging Assistance
- Check Application Insights for production issues
- Use structured logging to track request flows
- Verify database queries with EF Core logging
- Test external API integrations with mock responses
- Validate localization with culture switching

This solution represents a modern, scalable, and maintainable approach to building web applications with .NET, emphasizing clean architecture, comprehensive testing, and production-ready deployment practices.
