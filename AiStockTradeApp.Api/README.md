# Stock Data API - .NET Core 9 MVC Project

## Project Overview

Build a comprehensive stock data API using .NET Core 9 MVC architecture that functions similarly to Alpha Vantage API or Twelve Data. The API will store historical stock data in a SQL database using Entity Framework Core and provide endpoints for data retrieval and bulk uploads.

## Technology Stack

- **.NET Core 9** - Web API Framework
- **Entity Framework Core** - ORM for database operations
- **SQL Server** - Primary database (configurable for other providers)
- **AutoMapper** - Object mapping
- **Swagger/OpenAPI** - API documentation
- **FluentValidation** - Input validation
- **Serilog** - Structured logging

## Architecture Requirements

### MVC Pattern Implementation
- **Models**: Entity models, DTOs, and view models
- **Views**: Not applicable (API-only)
- **Controllers**: API controllers for stock data operations

### Project Structure
```
StockDataApi/
├── Controllers/
│   ├── StockController.cs
│   ├── HistoricalDataController.cs
│   └── UploadController.cs
├── Models/
│   ├── Entities/
│   │   ├── Stock.cs
│   │   ├── StockPrice.cs
│   │   └── StockMetadata.cs
│   ├── DTOs/
│   │   ├── StockDto.cs
│   │   ├── StockPriceDto.cs
│   │   └── BulkUploadDto.cs
│   └── Requests/
│       ├── StockDataRequest.cs
│       └── HistoricalDataRequest.cs
├── Services/
│   ├── IStockService.cs
│   ├── StockService.cs
│   ├── IDataUploadService.cs
│   └── DataUploadService.cs
├── Data/
│   ├── StockDbContext.cs
│   ├── Repositories/
│   └── Migrations/
├── Validators/
├── Mappings/
└── Extensions/
```

## Database Schema Requirements

### Core Entities

#### Stock Entity
```csharp
public class Stock
{
    public int Id { get; set; }
    public string Symbol { get; set; } // e.g., "AAPL"
    public string CompanyName { get; set; }
    public string Exchange { get; set; } // e.g., "NASDAQ"
    public string Sector { get; set; }
    public string Industry { get; set; }
    public string Currency { get; set; } // e.g., "USD"
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation Properties
    public ICollection<StockPrice> Prices { get; set; }
}
```

#### StockPrice Entity (Historical Data)
```csharp
public class StockPrice
{
    public long Id { get; set; }
    public int StockId { get; set; }
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal AdjustedClose { get; set; }
    public long Volume { get; set; }
    public decimal? DividendAmount { get; set; }
    public decimal? SplitCoefficient { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation Properties
    public Stock Stock { get; set; }
}
```

### Database Indexes
- Unique index on `Stock.Symbol`
- Composite index on `StockPrice.StockId` and `StockPrice.Date`
- Index on `StockPrice.Date` for time-range queries

## API Endpoints Specification

### Stock Management Endpoints

#### 1. Get Stock Information
```
GET /api/stocks/{symbol}
GET /api/stocks?symbols=AAPL,GOOGL,MSFT
```

#### 2. Search Stocks
```
GET /api/stocks/search?query={searchTerm}
```

### Historical Data Endpoints

#### 3. Get Time Series Data
```
GET /api/stocks/{symbol}/prices?interval=daily&outputsize=compact
GET /api/stocks/{symbol}/prices?from=2023-01-01&to=2023-12-31
```

**Query Parameters:**
- `interval`: daily, weekly, monthly
- `outputsize`: compact (last 100 data points), full (all available)
- `from`, `to`: Date range (YYYY-MM-DD format)

#### 4. Get Latest Quote
```
GET /api/stocks/{symbol}/quote
```

#### 5. Get Multiple Quotes
```
GET /api/quotes?symbols=AAPL,GOOGL,MSFT
```

### Data Upload Endpoints

#### 6. Upload CSV Data
```
POST /api/upload/csv
Content-Type: multipart/form-data
```

**CSV Format Requirements:**
```csv
Symbol,Date,Open,High,Low,Close,AdjustedClose,Volume,DividendAmount,SplitCoefficient
AAPL,2023-01-03,130.28,130.90,124.17,125.07,124.19,112117471,0,1
```

#### 7. Upload JSON Data
```
POST /api/upload/json
Content-Type: application/json
```

**JSON Format:**
```json
{
  "data": [
    {
      "symbol": "AAPL",
      "date": "2023-01-03",
      "open": 130.28,
      "high": 130.90,
      "low": 124.17,
      "close": 125.07,
      "adjustedClose": 124.19,
      "volume": 112117471,
      "dividendAmount": 0,
      "splitCoefficient": 1
    }
  ]
}
```

#### 8. Bulk Operations
```
POST /api/upload/bulk
DELETE /api/stocks/{symbol}/prices?from=2023-01-01&to=2023-12-31
```

## Key Features Implementation

### 1. Data Upload & Processing
- **CSV Parser**: Handle large CSV files with streaming
- **JSON Processor**: Batch processing for JSON uploads
- **Data Validation**: Validate ticker symbols, dates, and price data
- **Duplicate Handling**: Upsert logic for existing data points
- **Error Reporting**: Detailed validation errors and processing results

### 2. Historical Data Management
- **Time Series Storage**: Efficient storage of daily, weekly, monthly data
- **Data Integrity**: Ensure no gaps in historical data
- **Adjusted Prices**: Handle stock splits and dividends
- **Data Retention**: Configurable data retention policies

### 3. Query Optimization
- **Pagination**: Support for large result sets
- **Filtering**: Date ranges, multiple symbols
- **Caching**: Redis cache for frequently accessed data
- **Compression**: GZIP compression for large responses

### 4. Data Quality & Validation
- **Market Calendar**: Validate trading days
- **Price Validation**: Logical price checks (high >= low, etc.)
- **Volume Validation**: Non-negative volume checks
- **Corporate Actions**: Handle splits and dividends

## Configuration Requirements

### Database Configuration
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=StockDataApi;Trusted_Connection=true;TrustServerCertificate=true"
  }
}
```

### API Configuration
```json
{
  "ApiSettings": {
    "DefaultPageSize": 100,
    "MaxPageSize": 1000,
    "CacheExpirationMinutes": 15,
    "MaxUploadSizeBytes": 52428800
  }
}
```

## Performance Requirements

### Response Time Targets
- Individual stock quote: < 100ms
- Historical data (1 year): < 500ms
- Bulk upload (1000 records): < 5 seconds
- Search queries: < 200ms

### Scalability Considerations
- Support for 10,000+ stock symbols
- Handle 1M+ daily price records
- Concurrent upload processing
- Read-heavy workload optimization

## Security & Validation

### Input Validation
- Ticker symbol format validation
- Date range validation
- Numeric data validation
- File size and type restrictions

### Rate Limiting
- Implement rate limiting per API key
- Different limits for different endpoints
- Configurable rate limits

### Authentication (Optional)
- API key authentication
- JWT token support
- Role-based access control

## Testing Requirements

### Unit Tests
- Service layer testing
- Repository pattern testing
- Validation logic testing
- Mapping logic testing

### Integration Tests
- Database integration tests
- API endpoint tests
- File upload tests
- Performance tests

## Documentation Requirements

### API Documentation
- Swagger/OpenAPI specification
- Interactive API explorer
- Example requests/responses
- Error code documentation

### Development Documentation
- Setup instructions
- Database migration guide
- Deployment instructions
- Performance tuning guide

## Deployment Configuration

### Environment Setup
- Development, Staging, Production configurations
- Docker containerization support
- Health check endpoints
- Logging and monitoring setup

## Success Criteria

1. **Functional**: All endpoints working as specified
2. **Performance**: Meeting response time targets
3. **Reliability**: 99.9% uptime for data retrieval
4. **Scalability**: Handle expected data volumes
5. **Maintainability**: Clean, testable code architecture
6. **Documentation**: Complete API and setup documentation

## Getting Started

1. Clone the repository
2. Configure database connection string
3. Run Entity Framework migrations
4. Seed initial stock symbols (optional)
5. Start the API and navigate to Swagger UI
6. Test with sample CSV/JSON uploads

This API will serve as a comprehensive stock data platform capable of handling large-scale financial data operations while maintaining high performance and reliability standards.
