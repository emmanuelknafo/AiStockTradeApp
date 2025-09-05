# AiStockTradeApp.Api - REST API Backend

> Standardized Documentation Header (2025-09-05)
> Unified solution README conventions applied. New ADO test case description automation: `scripts/Update-AdoTestCaseDescriptions.ps1`.

## 🚀 Project Overview

The REST API backend for the AI Stock Trade Application built with .NET 9 Minimal API. This service provides comprehensive endpoints for stock data retrieval, historical price management, user watchlist operations, and background processing jobs.

## 🏗️ Architecture Role

This is the **API layer** of the clean architecture solution, serving as the central data hub that:

- **Processes business logic** through service layer integration
- **Manages data persistence** via Entity Framework Core
- **Provides REST endpoints** using Minimal API pattern
- **Handles background jobs** for data import and processing
- **Serves the UI project** and external MCP clients

### Key Responsibilities

- **Stock data retrieval** from external APIs (Alpha Vantage, Yahoo Finance, Twelve Data)
- **Historical price management** with CSV import capabilities
- **User watchlist operations** with persistent storage
- **Background job processing** for data imports and refresh
- **Health monitoring** and system diagnostics
- **API integration** for Model Context Protocol (MCP) server

## 🛠️ Technology Stack

### Core Framework

- **.NET 9 Minimal API** - Lightweight, high-performance API framework
- **Entity Framework Core 9** - ORM for database operations
- **SQL Server/Azure SQL** - Primary database with LocalDB support
- **ASP.NET Core Identity** - User authentication and authorization

### Background Processing

- **Hosted Services** - Background job processing
- **In-memory queues** - Job queue management
- **CSV processing** - Historical data import capabilities
- **Scheduled tasks** - Automated data refresh

### Monitoring & Observability

- **Application Insights** - Telemetry and performance monitoring
- **Structured logging** - Comprehensive request/response logging
- **Health checks** - Endpoint health monitoring
- **Swagger/OpenAPI** - API documentation and testing

## 📁 Project Structure

```text
AiStockTradeApp.Api/
├── Background/
│   ├── ImportJobModels.cs          # Job processing data models
│   ├── ImportJobProcessor.cs       # Background CSV import processor
│   └── ImportJobQueue.cs           # Job queue management
├── Middleware/
│   └── (Custom middleware implementations)
├── Properties/
│   └── launchSettings.json         # Development server configuration
├── ApiAssemblyMarker.cs            # Assembly marker for DI registration
├── Program.cs                      # Application entry point and API endpoints
├── TestStartup.cs                  # Test environment configuration
├── appsettings.json                # Application configuration
├── appsettings.Development.json    # Development-specific settings
└── Dockerfile                      # Container configuration
```

## 🔗 Dependencies

### Project References

- **AiStockTradeApp.Services** - Business logic and external API integration
- **AiStockTradeApp.DataAccess** - Entity Framework Core and repositories
- **AiStockTradeApp.Entities** - Domain models and data transfer objects

### Key Service Integrations

- **IStockDataService** - External stock API integration
- **IHistoricalPriceService** - Historical data management
- **IListedStockService** - Listed stocks catalog
- **IUserWatchlistService** - User-specific watchlist operations

## 🌐 API Endpoints

### Stock Data Management

#### Get Real-time Stock Quote

```http
GET /api/stocks/quote?symbol={symbol}
```

Retrieves current stock price, change, and market data.

#### Search Stock Symbols

```http
GET /api/stocks/search?query={searchTerm}
```

Search for stocks by company name or ticker symbol.

### Historical Data Management

#### Get Historical Prices

```http
GET /api/historical-prices/{symbol}?from=2025-08-01&to=2025-08-31&take=100
```

Retrieves historical price data within a date range.

#### Import Historical Data

```http
POST /api/historical-prices/{symbol}/import-csv
Content-Type: text/csv
```

Imports historical price data from CSV format with background processing.

### User Watchlist Operations

#### Get User Watchlist

```http
GET /api/watchlist
Authorization: Bearer {token}
```

Retrieve the authenticated user's watchlist.

#### Add Stock to Watchlist

```http
POST /api/watchlist
Authorization: Bearer {token}
```

### Health and Monitoring

#### Health Check

```http
GET /health
```

Returns application health status and dependency checks.

#### Version Information

```http
GET /api/version
```

Get application version and build information.

## ⚙️ Configuration

### Application Settings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=AiStockTradeApp;Trusted_Connection=true"
  },
  "AlphaVantage": {
    "ApiKey": "YOUR_API_KEY"
  },
  "TwelveData": {
    "ApiKey": "YOUR_API_KEY"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "AiStockTradeApp": "Debug"
    }
  }
}
```

## 🚀 Development Workflow

### Running the API

```bash
# Start the API server
dotnet run --project AiStockTradeApp.Api

# With specific environment
dotnet run --environment Development --project AiStockTradeApp.Api

# Using the start script (starts both API and UI)
.\scripts\start.ps1 -Mode Local
```

### Database Operations

```bash
# Add new migration
dotnet ef migrations add MigrationName --project AiStockTradeApp.DataAccess

# Update database
dotnet ef database update --project AiStockTradeApp.DataAccess
```

### API Testing

```bash
# Health check
curl https://localhost:7043/health

# Get stock quote
curl "https://localhost:7043/api/stocks/quote?symbol=AAPL"
```

## 🧪 Testing Support

### Test Configuration

The API supports testing scenarios with:

- **In-memory database** - Fast test execution
- **Mock external services** - Simulated stock data APIs
- **Test environment** - Isolated test configuration

## 🔒 Security Implementation

### Authentication & Authorization

- **JWT Bearer tokens** - Secure API authentication
- **Authorization policies** - Role-based access control
- **Input validation** - Model validation and sanitization

### API Security

- **HTTPS enforcement** - TLS encryption required
- **CORS configuration** - Cross-origin request control
- **Rate limiting** - API abuse prevention
- **Input sanitization** - SQL injection and XSS prevention

## 🔧 Troubleshooting

### Common Issues

#### Database Connection Problems

```bash
# Check connection string
dotnet user-secrets list --project AiStockTradeApp.Api

# Test database connectivity
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT @@VERSION"
```

#### External API Issues

- **Rate limits** - Check API key quotas
- **Authentication** - Verify API keys in configuration
- **Network connectivity** - Test external API endpoints

## 📦 Deployment

### Container Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80 443
```

### Environment Variables

- **ASPNETCORE_ENVIRONMENT** - Development/Production
- **ConnectionStrings__DefaultConnection** - Database connection
- **AlphaVantage__ApiKey** - Stock data API key
