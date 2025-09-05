# AiStockTradeApp - Web UI (MVC)

> Standardized Documentation Header (2025-09-05)
> Unified README format applied. See `scripts/Update-AdoTestCaseDescriptions.ps1` for Azure DevOps test case description automation; refer to root README for global architecture.

## 🎨 Project Overview

The main web application providing a modern, responsive user interface for the AI Stock Trade application. Built with ASP.NET Core MVC, this project delivers server-side rendered pages with real-time stock tracking, AI-powered analysis, and comprehensive user management.

## 🏗️ Architecture Role

This is the **presentation layer** of the clean architecture solution. It communicates with the REST API backend (AiStockTradeApp.Api) via HTTP clients and focuses on user experience, localization, and responsive design.

### Key Responsibilities

- **User Interface** - Razor views with Bootstrap 5 styling
- **User Authentication** - ASP.NET Core Identity integration
- **API Communication** - HTTP client integration with AiStockTradeApp.Api
- **Session Management** - User state and preferences
- **Localization** - Multi-language support (English/French)
- **Client-side Interactions** - JavaScript/jQuery enhancements

## 🚀 Technology Stack

### Core Framework

- **ASP.NET Core MVC 9** - Web framework with server-side rendering
- **ASP.NET Core Identity** - User authentication and authorization
- **Entity Framework Core** - Identity data storage
- **Dependency Injection** - Built-in IoC container

### Frontend Technologies

- **Razor Views** - Server-side templating with C# integration
- **Bootstrap 5** - Responsive CSS framework
- **JavaScript/jQuery** - Client-side interactions and AJAX
- **Chart.js** - Stock price visualization and charts
- **Font Awesome** - Professional icon library

### HTTP Integration

- **ApiStockDataServiceClient** - HTTP client for API communication
- **Polly** - Retry policies and resilience patterns
- **JSON Serialization** - API data exchange

## 📁 Project Structure

```
AiStockTradeApp/
├── Controllers/
│   ├── AccountController.cs        # User authentication and registration
│   ├── HomeController.cs           # Dashboard and main navigation
│   ├── ListedStocksController.cs   # Stock listing and management
│   ├── StockController.cs          # Stock operations and API integration
│   └── VersionController.cs        # Application version information
├── Views/
│   ├── Account/
│   │   ├── Login.cshtml            # User login page
│   │   ├── Register.cshtml         # User registration page
│   │   └── UserProfile.cshtml      # User profile management
│   ├── Home/
│   │   ├── Dashboard.cshtml        # Main stock dashboard
│   │   ├── Index.cshtml            # Landing page
│   │   └── Privacy.cshtml          # Privacy policy
│   ├── ListedStocks/
│   │   └── Index.cshtml            # Browse listed stocks
│   ├── Stock/
│   │   └── Dashboard.cshtml        # Stock management interface
│   └── Shared/
│       ├── _Layout.cshtml          # Main layout template
│       ├── _LoginPartial.cshtml    # Authentication partial view
│       └── Error.cshtml            # Error page template
├── Services/
│   ├── ApiStockDataServiceClient.cs # HTTP client for API communication
│   ├── SimpleStringLocalizer.cs    # Custom localization service
│   └── AuthenticationDiagnosticsService.cs # Auth debugging
├── Middleware/
│   └── (Custom middleware implementations)
├── ViewModels/
│   └── (Imported from AiStockTradeApp.Entities)
├── wwwroot/
│   ├── css/
│   │   ├── site.css               # Application-specific styles
│   │   └── stock-tracker.css      # Stock dashboard styling
│   ├── js/
│   │   ├── site.js                # General JavaScript functionality
│   │   └── stock-tracker.js       # Stock dashboard interactions
│   ├── lib/                       # Third-party libraries (Bootstrap, jQuery)
│   └── favicon.ico
├── Resources/
│   └── (Localization resource files)
├── Properties/
│   └── launchSettings.json        # Development server configuration
├── appsettings.json               # Application configuration
├── appsettings.Development.json   # Development-specific settings
├── Program.cs                     # Application entry point and DI setup
└── Dockerfile                     # Container configuration
```

## 🔗 Dependencies

### Project References

- **AiStockTradeApp.Services** - HTTP client services only (ApiStockDataServiceClient)
- **AiStockTradeApp.Entities** - View models and data transfer objects

### NuGet Packages

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
<PackageReference Include="Microsoft.Extensions.Http.Polly" />
<PackageReference Include="Polly" />
<PackageReference Include="FluentValidation.AspNetCore" />
```

## ⚙️ Configuration

### Application Settings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=AiStockTradeApp;Trusted_Connection=true"
  },
  "StockApi": {
    "BaseUrl": "https://localhost:7043"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Service Registration

```csharp
// Program.cs - Service configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Custom localization
builder.Services.AddSingleton<IStringLocalizer<SharedResource>, SimpleStringLocalizer>();

// HTTP client for API communication
builder.Services.AddHttpClient<ApiStockDataServiceClient>((sp, http) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["StockApi:BaseUrl"] ?? "https://localhost:7043";
    http.BaseAddress = new Uri(baseUrl);
});

// Service abstractions
builder.Services.AddScoped<IStockDataService, ApiStockDataServiceClient>();
builder.Services.AddScoped<IAIAnalysisService, AIAnalysisService>();
builder.Services.AddSingleton<IWatchlistService, WatchlistService>();
builder.Services.AddScoped<IUserWatchlistService, UserWatchlistService>();
```

## 🌐 Localization Implementation

### Supported Languages

- **English (en)** - Default language
- **French (fr)** - Secondary language

### Custom Localizer

The application uses a custom `SimpleStringLocalizer` implementation with embedded translations:

```csharp
// SimpleStringLocalizer.cs
public class SimpleStringLocalizer : IStringLocalizer<SharedResource>
{
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _translations;

    private void LoadTranslations()
    {
        _translations["en"] = new Dictionary<string, string>
        {
            ["Header_Title"] = "AI-Powered Stock Tracker",
            ["Btn_AddStock"] = "Add Stock",
            // ... more translations
        };

        _translations["fr"] = new Dictionary<string, string>
        {
            ["Header_Title"] = "Traqueur d'Actions IA",
            ["Btn_AddStock"] = "Ajouter Action",
            // ... more translations
        };
    }
}
```

### Usage in Views

```html
@using Microsoft.Extensions.Localization
@inject IStringLocalizer<SharedResource> Localizer

@{
    ViewData["Title"] = Localizer["Header_Title"];
}

<h1>@Localizer["Dashboard_Title"]</h1>
<button class="btn btn-primary">@Localizer["Btn_AddStock"]</button>
```

### Culture Switching

- **Cookie-based persistence** - Culture stored for one year
- **POST action** - `HomeController.SetLanguage` endpoint
- **Automatic detection** - Request localization middleware

## 🎯 Key Features

### User Authentication

- **Registration** - New user account creation
- **Login/Logout** - Session-based authentication
- **User Profile** - Account management and preferences
- **Password Management** - Secure password handling
- **Remember Me** - Persistent login sessions

### Stock Management

- **Search and Add** - Stock symbol lookup and watchlist addition
- **Real-time Updates** - Live price data from API
- **Portfolio View** - Multi-stock overview with performance metrics
- **Historical Charts** - Price movement visualization
- **Export/Import** - Watchlist data portability

### User Experience

- **Responsive Design** - Mobile-first approach with Bootstrap 5
- **Interactive Dashboard** - Dynamic stock cards with real-time updates
- **Theme Support** - Light/dark mode toggle
- **Auto-refresh** - Configurable data refresh intervals
- **Error Handling** - Graceful degradation and user feedback

### AI Integration

- **Analysis Display** - AI-generated insights and recommendations
- **Sentiment Indicators** - Visual representation of market sentiment
- **Investment Recommendations** - Buy/Hold/Sell suggestions
- **Performance Summaries** - Historical analysis and trends

## 🔄 API Integration Patterns

### HTTP Client Configuration

```csharp
// ApiStockDataServiceClient.cs
public class ApiStockDataServiceClient : IStockDataService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiStockDataServiceClient> _logger;

    public async Task<StockData?> GetStockDataAsync(string symbol)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/stocks/quote?symbol={symbol}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<StockData>(json, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stock data for {Symbol}", symbol);
        }
        return null;
    }
}
```

### Error Handling Strategy

```csharp
// Controller level error handling
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

## 🎨 Frontend Architecture

### JavaScript Organization

```javascript
// stock-tracker.js
class StockTracker {
    constructor() {
        this.refreshInterval = null;
        this.watchlist = [];
        this.init();
    }

    async addStock(symbol) {
        try {
            const response = await fetch(`/Stock/AddStock?symbol=${symbol}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': this.getAntiForgeryToken()
                }
            });
            
            if (response.ok) {
                await this.refreshWatchlist();
                this.showSuccessMessage(`Added ${symbol} to watchlist`);
            }
        } catch (error) {
            this.showErrorMessage('Failed to add stock');
        }
    }
}
```

### CSS Architecture

```css
/* stock-tracker.css */
.stock-card {
    transition: all 0.3s ease;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.stock-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 8px rgba(0,0,0,0.2);
}

.price-positive {
    color: #28a745;
}

.price-negative {
    color: #dc3545;
}
```

## 🚀 Development Workflow

### Running the Application

```bash
# Start the application (requires API to be running)
dotnet run --project AiStockTradeApp

# With specific environment
dotnet run --environment Development

# Using the start script (recommended)
.\scripts\start.ps1 -Mode Local
```

### Database Migrations

```bash
# Add new migration (from DataAccess project)
dotnet ef migrations add MigrationName --project AiStockTradeApp.DataAccess

# Update database
dotnet ef database update --project AiStockTradeApp.DataAccess
```

### Adding New Features

1. **Create view model** in AiStockTradeApp.Entities
2. **Add controller action** with proper error handling
3. **Create Razor view** with localization support
4. **Add JavaScript** for client-side interactions
5. **Update CSS** for styling
6. **Add unit tests** for controller logic
7. **Add UI tests** for user interactions

## 🧪 Testing Support

### Test Configuration

The UI project supports testing scenarios with:

- **In-memory database** - Fast test execution
- **Test user seeding** - Predefined test accounts
- **Mock API responses** - Simulated backend data
- **Environment variables** - Test-specific configuration

### UI Test Integration

```csharp
// Test-specific configuration
if (Environment.GetEnvironmentVariable("USE_INMEMORY_DB") == "true")
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("TestDb"));
}
```

## 📦 Deployment Considerations

### Container Support

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["AiStockTradeApp/AiStockTradeApp.csproj", "AiStockTradeApp/"]
RUN dotnet restore "AiStockTradeApp/AiStockTradeApp.csproj"
```

### Environment Configuration

- **Development** - LocalDB and local API
- **Testing** - In-memory database and mock services
- **Production** - Azure SQL and Azure App Service

## 🔒 Security Features

### Authentication Security

- **ASP.NET Core Identity** - Secure user authentication
- **Password requirements** - Configurable complexity rules
- **Account lockout** - Brute force protection
- **Email confirmation** - Account verification

### Application Security

- **CSRF protection** - Anti-forgery tokens in forms
- **XSS prevention** - Input encoding and validation
- **HTTPS enforcement** - TLS encryption required
- **Secure headers** - Security-related HTTP headers

### API Security

- **HTTP client authentication** - Secure API communication
- **Request validation** - Input sanitization
- **Error handling** - No sensitive information exposure
- **Rate limiting** - Client-side request throttling

## 🔧 Troubleshooting

### Common Issues

#### API Connection Errors

```bash
# Verify API is running
curl https://localhost:7043/health

# Check configuration
dotnet user-secrets list --project AiStockTradeApp
```

#### Database Issues

```bash
# Reset database
dotnet ef database drop --project AiStockTradeApp.DataAccess
dotnet ef database update --project AiStockTradeApp.DataAccess
```

#### Localization Problems

- Verify `SimpleStringLocalizer.cs` has all required keys
- Check culture configuration in `Program.cs`
- Test language switching functionality

## 📈 Performance Optimization

### Caching Strategy

- **Memory caching** - Frequently accessed data
- **Response caching** - Static content
- **Client-side caching** - Browser storage

### Bundle Optimization

- **CSS bundling** - Minimized stylesheets
- **JavaScript bundling** - Optimized scripts
- **Image optimization** - Compressed assets

## 🔄 Maintenance

### Regular Updates

- **NuGet packages** - Monthly security updates
- **Frontend libraries** - Bootstrap, jQuery updates
- **Localization** - New translation keys
- **Performance monitoring** - Response time analysis

### Monitoring

- **Application Insights** - Performance telemetry
- **Health checks** - Application status monitoring
- **Error tracking** - Exception logging and analysis
├── Controllers/
│   ├── AccountController.cs        # User authentication and account management
│   ├── DiagnosticsController.cs    # System diagnostics and health checks
│   ├── HomeController.cs           # Dashboard and main pages
│   ├── ListedStocksController.cs   # Listed stocks and company information
│   ├── StockController.cs          # Stock data and watchlist operations
│   ├── UserStockController.cs      # User-specific stock management
│   └── VersionController.cs        # Application version information
├── Views/                          # Razor view templates
│   ├── Home/                      # Dashboard views
│   ├── Stock/                     # Stock-related views
│   ├── Account/                   # Authentication views
│   └── Shared/                    # Shared layouts and partials
├── wwwroot/                       # Static assets (CSS, JS, images)
├── Services/
│   └── SimpleStringLocalizer.cs   # Custom localization service
├── ViewModels/                    # Data transfer objects for views
├── Middleware/                    # Custom middleware components
└── Resources/                     # Localization resource files
```
├── Controllers/
│   ├── StockController.cs       # Main stock operations
│   ├── HomeController.cs        # Dashboard and redirects
│   ├── AccountController.cs     # User account management
│   ├── ListedStocksController.cs # Listed stocks data
│   ├── VersionController.cs     # Application version info
│   └── DiagnosticsController.cs # System diagnostics
├── ViewModels/
│   ├── DashboardViewModel.cs    # Dashboard data models
│   ├── StockViewModel.cs        # Stock display models
│   └── ErrorViewModel.cs        # Error page models
├── Services/
│   └── ApiStockDataServiceClient.cs # HTTP client for API calls
├── Views/
│   ├── Stock/                   # Stock-related views
│   ├── Home/                    # Dashboard views
│   ├── Account/                 # Account management views
│   └── Shared/                  # Shared layouts and partials
├── Resources/                   # Localization resources
├── Middleware/                  # Custom middleware
├── wwwroot/                     # Static assets (CSS, JS, images)
│   ├── css/                     # Custom stylesheets
│   ├── js/                      # JavaScript files
│   └── lib/                     # Third-party libraries
└── Properties/                  # Launch settings
```

## 🔧 Setup & Installation

### Prerequisites
- .NET 9 SDK or later
- Visual Studio 2022 or VS Code
- Internet connection for stock data APIs

### Configuration

1. **Clone the repository**
   ```bash
   git clone https://github.com/emmanuelknafo/AiStockTradeApp.git
   cd AiStockTradeApp/AiStockTradeApp
   ```

2. **Configure API Keys** (Optional but recommended)
   
   Update `appsettings.json` with your API keys:
   ```json
   {
     "AlphaVantage": {
       "ApiKey": "YOUR_ALPHA_VANTAGE_API_KEY"
     },
     "TwelveData": {
       "ApiKey": "YOUR_TWELVE_DATA_API_KEY"
     }
   }
   ```

3. **Get Free API Keys**
   - [Alpha Vantage](https://www.alphavantage.co/support/#api-key) - Primary data source
   - [Twelve Data](https://twelvedata.com/pricing) - Backup source (800 requests/day free)

### Running the Application

#### Using Visual Studio
1. Open `AiStockTradeApp.sln`
2. Set `AiStockTradeApp` as startup project
3. Press F5 or click "Start Debugging"

#### Using Command Line
```bash
dotnet restore
dotnet run --project AiStockTradeApp.csproj
```

#### Using Docker
```bash
docker build -t ai-stock-tracker .
docker run -p 8080:8080 ai-stock-tracker
```

The application will be available at `https://localhost:5001` or `http://localhost:5000`

## 📱 Usage

### Adding Stocks
1. Use the search bar to enter a stock ticker symbol (e.g., "AAPL")
2. Select from auto-suggestions or press Enter
3. Stock will be added to your watchlist with current data

### Viewing Analysis
1. Click on any stock card to view detailed information
2. AI analysis includes trend analysis and sentiment
3. Recommendations show Buy/Hold/Sell with reasoning

### Managing Watchlist
- **Remove Stock**: Click the X button on any stock card
- **Clear All**: Use the "Clear Watchlist" button
- **Export Data**: Download your watchlist in CSV or JSON format

## 🔌 API Integration

### Data Sources (with automatic failover)
1. **Alpha Vantage** (Primary) - Real-time data with API key
2. **Yahoo Finance** (Fallback) - Free, no API key required  
3. **Twelve Data** (Fallback) - Free tier with optional API key

### Fallback Strategy
If the primary API fails, the application automatically switches to backup sources to ensure continuous data availability.

## 🚀 Deployment

### Azure Deployment
The application includes Azure deployment scripts and Bicep templates:
- Infrastructure as Code with `../infrastructure/main.bicep`
- CI/CD workflows for automated deployment
- Container support with included Dockerfile

### Environment Variables
```bash
ASPNETCORE_ENVIRONMENT=Production
ALPHA_VANTAGE_API_KEY=your_key_here
TWELVE_DATA_API_KEY=your_key_here
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

- **Issues**: Report bugs or request features via [GitHub Issues](https://github.com/emmanuelknafo/AiStockTradeApp/issues)
- **Documentation**: Check the `/docs` folder for detailed guides
- **API Limits**: Be aware of rate limits on free API tiers

## 🎯 Roadmap

- [ ] Real-time WebSocket updates
- [ ] User authentication and persistent watchlists
- [ ] Advanced AI portfolio optimization
- [ ] Mobile app companion
- [ ] Social features and shared watchlists

---

**Disclaimer**: This application is for educational and informational purposes only. It should not be considered as financial advice. Always consult with a qualified financial advisor before making investment decisions.
