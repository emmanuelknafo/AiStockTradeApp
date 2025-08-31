# AI-Powered Stock Tracker

An intelligent ASP.NET Core MVC web application that provides real-time stock tracking with AI-powered analysis and investment recommendations.

## 🚀 Overview

This application combines traditional stock market data with artificial intelligence to help users make informed investment decisions. Track your favorite stocks, get real-time price updates, and receive AI-generated insights and recommendations.

## ✨ Key Features

### 📊 **Stock Tracking**
- **Real-time Data**: Live stock prices, changes, and percentage movements
- **Personal Watchlist**: Add/remove stocks using ticker symbols (AAPL, GOOGL, etc.)
- **Portfolio View**: Monitor multiple stocks simultaneously
- **Visual Indicators**: Color-coded gains (green) and losses (red)
- **Historical Charts**: Track price movements over time

### 🤖 **AI-Powered Intelligence**
- **Smart Analysis**: AI-generated trend analysis and market sentiment
- **Investment Recommendations**: Buy/Hold/Sell suggestions with detailed reasoning
- **Performance Summaries**: AI creates insights on stock performance over various timeframes
- **Intelligent Alerts**: Smart notifications based on AI analysis patterns

### 🎨 **User Experience**
- **Responsive Design**: Works seamlessly on desktop and mobile devices
- **Modern Interface**: Clean, professional dashboard with intuitive stock cards
- **Theme Support**: Dark/light mode toggle for user preference
- **Smart Search**: Auto-suggestions when adding new stocks
- **Customizable Settings**: Personalized dashboard configuration

### 🔧 **Technical Features**
- **Multiple Data Sources**: Automatic failover between Alpha Vantage, Yahoo Finance, and Twelve Data
- **Session Management**: Persistent watchlists during user sessions
- **Auto-refresh**: Automatic stock price updates
- **Data Export/Import**: CSV and JSON format support
- **Price Alerts**: Configurable notifications for price thresholds
- **Robust Error Handling**: Graceful handling of API failures

## 🏗️ Architecture

### Technology Stack
- **Backend**: ASP.NET Core MVC (.NET 9)
- **Frontend**: Razor Views with custom CSS/JavaScript
- **APIs**: Multiple stock data providers for reliability
- **Session Storage**: In-memory session management

### Project Structure

```
AiStockTradeApp/
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
