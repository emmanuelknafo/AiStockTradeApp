# AI-Powered Stock Tracker MVC Application

This is an ASP.NET Core MVC implementation of the AI-powered stock tracker application. It provides the same functionality as the JavaScript version but built using server-side rendering with C# and Razor views.

## Features

### Core Functionality
- Search and add stocks to a watchlist by ticker symbol
- Display real-time or recent stock prices, price changes, and percentage changes
- Show a portfolio view with multiple stocks
- Remove stocks from the watchlist
- Clear entire watchlist

### AI-Powered Features
- Generate AI analysis/insights for each stock (trend analysis, basic sentiment)
- Provide AI-generated buy/hold/sell recommendations with reasoning
- Create AI summaries of stock performance over different time periods
- Smart alerts or notifications based on AI analysis

### UI Features
- Clean, professional dashboard layout
- Stock cards/tiles showing key metrics
- Color-coded indicators (green/red for gains/losses)
- Responsive design that works on mobile and desktop
- Search bar with auto-suggestions for adding new stocks
- Dark/light theme toggle
- Settings panel for customization

### Technical Features
- Built with ASP.NET Core MVC (.NET 9)
- Integrates with multiple stock APIs (Alpha Vantage, Yahoo Finance, Twelve Data)
- Session-based user state management
- Auto-refresh functionality
- Error handling for API failures
- Export watchlist functionality (CSV/JSON)
- Import data functionality
- Price alerts system

## Architecture

### Models
- `StockData.cs` - Core stock data model with price, change, AI analysis
- `ViewModels.cs` - Dashboard, user settings, and export models

### Controllers
- `StockController.cs` - Main controller handling all stock operations
- `HomeController.cs` - Redirects to stock dashboard

### Services
- `StockDataService.cs` - Handles external API calls to fetch stock data
- `AIAnalysisService.cs` - Generates AI-powered analysis and recommendations
- `WatchlistService.cs` - Manages user watchlists and portfolio calculations

### Views
- `Dashboard.cshtml` - Main stock tracker interface
- Custom CSS (`stock-tracker.css`) - Styling based on original JavaScript app
- Custom JavaScript (`stock-tracker.js`) - Frontend interactions

## API Integration

The application supports multiple stock data sources with automatic fallback:

1. **Alpha Vantage** (Primary) - Real-time data with API key
2. **Yahoo Finance** (Fallback) - Free, no API key required
3. **Twelve Data** (Fallback) - Free tier available

### Configuration

Update `appsettings.json` with your Alpha Vantage API key:

```json
{
  "AlphaVantage": {
    "ApiKey": "YOUR_ALPHA_VANTAGE_API_KEY"
  }
}
```

Get a free API key from [Alpha Vantage](https://www.alphavantage.co/support/#api-key).

## Building and Running

### Prerequisites
- .NET 9 SDK
- Visual Studio 2022 or VS Code

### Commands

```bash
# Restore dependencies
dotnet restore

# Build the application
dotnet build

# Run the application
dotnet run

# Run with specific environment
dotnet run --environment Development
```

The application will be available at `https://localhost:5001` or `http://localhost:5000`.

## Usage

1. **Add Stocks**: Type a ticker symbol (e.g., AAPL) and click "Add Stock"
2. **Search Suggestions**: Get popular stock suggestions as you type
3. **View Analysis**: Each stock card shows price, change, and AI-generated analysis
4. **Set Alerts**: Double-click on a stock card to set price alerts
5. **Auto-refresh**: Enable automatic data refresh in settings
6. **Export Data**: Export your watchlist as CSV or JSON
7. **Import Data**: Import previously exported watchlist data
8. **Theme Toggle**: Switch between light and dark themes

## Comparison with JavaScript Version

This MVC implementation provides the same features as the JavaScript version but with:

### Advantages
- **Server-side rendering** for better SEO and initial load performance
- **Type safety** with C# models and strong typing
- **Better error handling** with structured exception management
- **Session management** for user state persistence
- **Scalable architecture** with dependency injection
- **Security** with CSRF protection and server-side validation

### Architecture Differences
- **Backend API**: Custom controllers instead of client-side API calls
- **State Management**: Server-side sessions instead of localStorage
- **Data Processing**: Server-side AI analysis instead of client-side
- **File Operations**: Server-side export/import instead of client-side blob operations

## Development Conventions

- **Models**: Located in `Models/` folder, represent data structures
- **Controllers**: Located in `Controllers/`, handle HTTP requests and business logic
- **Services**: Located in `Services/`, contain business logic and external API integration
- **Views**: Located in `Views/`, contain Razor templates for UI
- **Static Assets**: Located in `wwwroot/`, contain CSS, JavaScript, and other static files

## Testing

The application includes error handling and fallback mechanisms for:
- API failures and rate limiting
- Invalid stock symbols
- Network connectivity issues
- Session timeouts
- File import/export errors

## Future Enhancements

Potential improvements that could be added:
- Database persistence instead of in-memory storage
- User authentication and individual user accounts
- Real-time WebSocket updates
- Advanced charting with historical data
- News feed integration
- More sophisticated AI analysis using external AI APIs
- Unit and integration tests
- Docker containerization
- Cloud deployment configurations

## API Keys and Environment Variables

For production deployment, consider using:
- Azure Key Vault for API key storage
- Environment variables for configuration
- Application Insights for logging and monitoring
- Azure App Service for hosting

## License

This project is for educational and demonstration purposes.
