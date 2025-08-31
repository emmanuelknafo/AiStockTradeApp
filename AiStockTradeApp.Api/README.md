# AiStockTradeApp.Api - REST API Backend

## Project Overview

The API backend for the AI Stock Trade Application built with .NET 9 Minimal API. This service provides REST endpoints for stock data retrieval, historical price management, and background processing jobs.

## Technology Stack

- **.NET 9** - Minimal API Framework
- **Entity Framework Core** - ORM for database operations
- **SQL Server/Azure SQL** - Primary database
- **Swagger/OpenAPI** - API documentation  
- **Application Insights** - Telemetry and monitoring
- **Background Services** - Hosted background job processing

## Current Implementation

### Project Structure
```
AiStockTradeApp.Api/
├── Background/
│   ├── ImportJobModels.cs      # Job processing models
│   └── ImportJobProcessor.cs   # Background CSV import processor
├── Middleware/
│   └── (Custom middleware implementations)
├── Properties/
│   └── launchSettings.json
├── ApiAssemblyMarker.cs        # Assembly marker for DI
├── Program.cs                  # Application entry point and configuration
├── appsettings.json            # Configuration settings
├── appsettings.Development.json
└── Dockerfile                  # Container configuration
```

## Current API Endpoints

### Historical Data Management

#### Get Historical Prices

```http
GET /api/historical-prices/{symbol}?from=2025-08-01&to=2025-08-19&take=30
```

Retrieves historical price data for a stock symbol within a date range.

**Parameters:**
- `symbol` - Stock ticker symbol (e.g., AAPL, GOOGL)
- `from` - Start date (YYYY-MM-DD format)
- `to` - End date (YYYY-MM-DD format)  
- `take` - Maximum number of records to return

#### Import CSV Data

```http
POST /api/historical-prices/{symbol}/import-csv
Content-Type: text/csv or text/plain
```

Imports historical price data from CSV format. Supports NASDAQ-style CSV with columns:
Date, Close/Last, Volume, Open, High, Low

**Headers:**
- `X-File-Name` (optional) - Original filename for tracking

**Response:**
- `202 Accepted` with job status location for tracking import progress

### Background Processing

The API includes background job processing for:

- **CSV Import Jobs** - Asynchronous processing of uploaded CSV files
- **Data Validation** - Ensures data integrity during imports
- **Progress Tracking** - Monitor import job status and completion

## Configuration

### Database Connection

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=StockTracker;Trusted_Connection=true;TrustServerCertificate=true"
  }
}
```

### Application Insights

```json
{
  "ApplicationInsights": {
    "ConnectionString": "Your-Application-Insights-Connection-String"
  }
}
```

## Dependencies

The API project references:

- **AiStockTradeApp.Entities** - Domain models and DTOs
- **AiStockTradeApp.DataAccess** - Entity Framework data access layer  
- **AiStockTradeApp.Services** - Business logic and external API integrations

## Running the API

### Development

```bash
# From the API project directory
dotnet run

# Or from solution root
dotnet run --project AiStockTradeApp.Api
```

The API will be available at:
- HTTPS: `https://localhost:7043`
- HTTP: `http://localhost:5043`

### Docker

```bash
# Build container
docker build -t ai-stock-api .

# Run container
docker run -p 5043:8080 ai-stock-api
```

## API Documentation

When running in Development mode, Swagger UI is available at:
- `https://localhost:7043/swagger`

The OpenAPI specification provides interactive documentation for all available endpoints.

## Background Services

### Import Job Processor

Handles asynchronous processing of CSV data imports:

- **Job Queuing** - Accepts import requests and queues for processing
- **Data Parsing** - Validates and parses CSV data
- **Batch Processing** - Efficiently inserts data in batches
- **Error Handling** - Logs and reports processing errors
- **Progress Tracking** - Updates job status throughout processing

## Security Features

- **HTTPS Enforcement** - All endpoints require secure connections
- **Input Validation** - Validates all incoming data
- **Error Handling** - Secure error responses without sensitive information
- **CORS Configuration** - Configurable cross-origin request policies

## Monitoring and Logging

- **Application Insights Integration** - Comprehensive telemetry
- **Structured Logging** - Detailed logging with correlation IDs
- **Health Checks** - Endpoint health monitoring
- **Performance Metrics** - Request timing and throughput tracking

## Testing

Run API tests from the solution root:

```bash
# Unit tests
dotnet test AiStockTradeApp.Tests

# Integration tests specifically for API
dotnet test --filter "Category=Integration"
```

## Deployment

### Azure App Service

The API is designed for deployment to Azure App Service with:

- **Managed Identity** - Secure access to Azure resources
- **Application Insights** - Built-in monitoring
- **Auto-scaling** - Automatic scaling based on demand
- **Health Checks** - Automated health monitoring

### Environment Variables

Set these environment variables for production:

```bash
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection="Your-SQL-Connection-String"
ApplicationInsights__ConnectionString="Your-AI-Connection-String"
```

## Contributing

When adding new endpoints:

1. Follow RESTful conventions
2. Add appropriate validation
3. Include comprehensive error handling
4. Update Swagger documentation
5. Add corresponding unit tests
6. Ensure proper logging

## Related Projects

- **[AiStockTradeApp](../AiStockTradeApp/)** - Web UI that consumes this API
- **[AiStockTradeApp.Entities](../AiStockTradeApp.Entities/)** - Shared domain models
- **[AiStockTradeApp.DataAccess](../AiStockTradeApp.DataAccess/)** - Data access layer
- **[AiStockTradeApp.Services](../AiStockTradeApp.Services/)** - Business services
