# Stock Data Caching with Azure SQL Database

This application implements a smart caching layer using Azure SQL Database and Entity Framework Core to reduce API calls and improve performance.

## How It Works

### 1. Cache-First Architecture
- **Cache Hit**: Data is served directly from the database if it exists and is not expired
- **Cache Miss**: Data is fetched from external APIs (Alpha Vantage, Yahoo Finance, Twelve Data) and then cached
- **Automatic Expiration**: Cached data expires after 15 minutes (configurable)

### 2. Database Schema
The `StockData` table stores:
- **Basic Stock Info**: Symbol, Price, Change, Company Name
- **AI Analysis**: Recommendations and analysis data
- **Chart Data**: Historical price data (stored as JSON)
- **Cache Metadata**: Cached timestamp and duration

### 3. Smart Caching Logic
```csharp
// 1. Try cache first
var cachedData = await _repository.GetCachedStockDataAsync(symbol);
if (cachedData != null && cachedData.IsCacheValid)
{
    return cachedData; // Return from cache
}

// 2. Fetch from APIs if cache miss/expired
var apiData = await FetchFromAPIs(symbol);
if (apiData.Success)
{
    await _repository.SaveStockDataAsync(apiData); // Cache the result
    return apiData;
}
```

## Configuration

### Connection Strings
- **Development**: LocalDB for local development
- **Azure**: Azure SQL Database with managed identity

### Cache Settings
```json
{
  "Cache": {
    "DefaultDurationMinutes": 15,
    "MaxHistoryDays": 30
  }
}
```

## Azure Infrastructure

### SQL Server Resources
- **SQL Server**: `sql-aistock-{env}-{instance}`
- **SQL Database**: `sqldb-aistock-{env}-{instance}`
- **Firewall Rules**: Allow Azure services
- **Connection Strings**: Stored in Key Vault

### Key Vault Integration
- SQL connection strings are stored securely in Azure Key Vault
- Web App references Key Vault secrets via managed identity

## Background Services

### Cache Cleanup Service
- Runs every 6 hours
- Removes cache entries older than 30 days
- Prevents database bloat

## Benefits

1. **Reduced API Costs**: Fewer calls to external stock APIs
2. **Improved Performance**: Fast database queries vs. network calls
3. **Better Reliability**: Cached data available even if APIs are down
4. **Cost Effective**: Basic Azure SQL tier is cost-efficient for caching
5. **Scalable**: Can handle multiple instances sharing the same cache

## Monitoring

- **Application Insights**: Tracks cache hit/miss ratios
- **Database Metrics**: Monitor query performance and storage usage
- **Logging**: Detailed logs for cache operations and cleanup

## Migration Commands

```bash
# Add migration
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update

# Remove migration
dotnet ef migrations remove
```

## Environment Variables

Required for Azure deployment:
- `ALPHA_VANTAGE_API_KEY`: API key for Alpha Vantage
- `TWELVE_DATA_API_KEY`: API key for Twelve Data
