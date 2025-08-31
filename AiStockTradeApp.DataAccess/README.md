# AiStockTradeApp.DataAccess

Data access layer for the AiStockTradeApp solution. Contains Entity Framework Core DbContext, migrations, repository interfaces and implementations used to persist and query stock and historical price data.

## Project Structure

```
AiStockTradeApp.DataAccess/
├── Data/
│   ├── StockDataContext.cs         # EF Core DbContext
│   └── (Seed data and configurations)
├── Migrations/
│   └── (EF Core migrations for schema versioning)
├── Interfaces/
│   ├── IStockDataRepository.cs     # Stock data repository interface
│   ├── IHistoricalPriceRepository.cs # Historical price repository interface
│   └── (Other repository interfaces)
├── Repositories/
│   ├── StockDataRepository.cs      # Stock data repository implementation
│   ├── HistoricalPriceRepository.cs # Historical price repository implementation
│   └── (Other repository implementations)
├── DesignTime/
│   └── StockDataContextFactory.cs # Design-time factory for migrations
└── AiStockTradeApp.DataAccess.csproj
```

## Database Entities

### Core Models

The data access layer manages the following core entities:

- **StockData** - Current stock information and real-time data
- **HistoricalPrice** - Historical stock price data points
- **WatchlistItem** - User watchlist entries
- **CachedStockData** - Performance optimization cache entries

### Entity Relationships

```csharp
// Example entity structure
public class StockData
{
    public string Symbol { get; set; }          // Primary key (e.g., "AAPL")
    public decimal CurrentPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public string? AIAnalysis { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Navigation properties
    public ICollection<HistoricalPrice> HistoricalPrices { get; set; }
}

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
    
    // Navigation properties
    public StockData Stock { get; set; }
}
```

## Repository Pattern

### Interface-Based Design

All data access follows the repository pattern with interfaces:

```csharp
public interface IStockDataRepository
{
    Task<StockData?> GetBySymbolAsync(string symbol);
    Task<IEnumerable<StockData>> GetMultipleBySymbolsAsync(IEnumerable<string> symbols);
    Task SaveAsync(StockData stockData);
    Task DeleteAsync(string symbol);
}
```

### Implementation Features

- **Async/await patterns** - All operations are asynchronous
- **Generic repository base** - Common CRUD operations
- **Query optimization** - Efficient database queries
- **Transaction support** - Unit of work pattern
- **Error handling** - Comprehensive exception management

## Database Configuration

### Connection Strings

#### Development (LocalDB)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=StockTracker;Trusted_Connection=true;TrustServerCertificate=true"
  }
}
```

#### Production (Azure SQL)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:your-server.database.windows.net,1433;Database=StockTracker;Authentication=Active Directory Default;"
  }
}
```

### DbContext Configuration

```csharp
public class StockDataContext : DbContext
{
    public StockDataContext(DbContextOptions<StockDataContext> options) : base(options) { }
    
    public DbSet<StockData> StockData { get; set; }
    public DbSet<HistoricalPrice> HistoricalPrices { get; set; }
    public DbSet<WatchlistItem> WatchlistItems { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Entity configurations
        modelBuilder.Entity<StockData>()
            .HasKey(s => s.Symbol);
            
        modelBuilder.Entity<HistoricalPrice>()
            .HasIndex(h => new { h.Symbol, h.Date })
            .IsUnique();
    }
}
```

## Entity Framework Migrations

### Managing Schema Changes

#### Add New Migration
```bash
# From the DataAccess project directory
dotnet ef migrations add MigrationName --project AiStockTradeApp.DataAccess

# Or from solution root
dotnet ef migrations add MigrationName --project AiStockTradeApp.DataAccess --startup-project AiStockTradeApp.Api
```

#### Update Database
```bash
# Apply migrations to database
dotnet ef database update --project AiStockTradeApp.DataAccess

# Apply specific migration
dotnet ef database update MigrationName --project AiStockTradeApp.DataAccess
```

#### Generate SQL Scripts
```bash
# Generate SQL script for migrations
dotnet ef migrations script --project AiStockTradeApp.DataAccess

# Generate script for specific range
dotnet ef migrations script FromMigration ToMigration --project AiStockTradeApp.DataAccess
```

### Migration Best Practices

1. **Test migrations** in development before applying to production
2. **Backup databases** before running migrations in production
3. **Review generated SQL** for performance implications
4. **Use explicit migration names** that describe the changes
5. **Avoid data loss** by carefully planning schema changes

## Performance Optimization

### Database Indexes

```sql
-- Key indexes for query performance
CREATE INDEX IX_HistoricalPrices_Symbol_Date ON HistoricalPrices (Symbol, Date);
CREATE INDEX IX_WatchlistItems_SessionId ON WatchlistItems (SessionId);
CREATE INDEX IX_StockData_LastUpdated ON StockData (LastUpdated);
```

### Query Optimization

```csharp
// Efficient querying with projections
public async Task<IEnumerable<StockSummaryDto>> GetStockSummariesAsync(IEnumerable<string> symbols)
{
    return await _context.StockData
        .Where(s => symbols.Contains(s.Symbol))
        .Select(s => new StockSummaryDto
        {
            Symbol = s.Symbol,
            CurrentPrice = s.CurrentPrice,
            Change = s.Change,
            ChangePercent = s.ChangePercent
        })
        .ToListAsync();
}
```

## Testing Support

### In-Memory Database

For unit testing, the project supports in-memory database:

```csharp
public class TestStockDataContext : StockDataContext
{
    public TestStockDataContext(DbContextOptions<StockDataContext> options) : base(options) { }
    
    public static StockDataContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<StockDataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        return new StockDataContext(options);
    }
}
```

### Test Data Seeding

```csharp
public static class TestDataSeeder
{
    public static void SeedTestData(StockDataContext context)
    {
        context.StockData.AddRange(
            new StockData { Symbol = "AAPL", CurrentPrice = 150.00m, Change = 2.50m },
            new StockData { Symbol = "GOOGL", CurrentPrice = 2800.00m, Change = -15.00m }
        );
        context.SaveChanges();
    }
}
```

## Build & Deployment

### Local Development

```bash
# Build the project
dotnet build AiStockTradeApp.DataAccess

# Run from solution root
dotnet build
```

### Package Management

Key NuGet packages:
- **Microsoft.EntityFrameworkCore.SqlServer** - SQL Server provider
- **Microsoft.EntityFrameworkCore.InMemory** - In-memory provider for testing
- **Microsoft.EntityFrameworkCore.Tools** - Migration tools
- **Microsoft.EntityFrameworkCore.Design** - Design-time services

## Environment-Specific Configurations

### Development
- Uses LocalDB for development
- Enables detailed logging
- Automatic migrations on startup

### Testing
- Uses in-memory database
- Isolated test data
- No persistent storage

### Production
- Uses Azure SQL Database
- Connection pooling enabled
- Optimized for performance
- Health checks configured

## Troubleshooting

### Common Issues

#### Migration Errors
```bash
# Reset migrations (development only)
dotnet ef database drop --project AiStockTradeApp.DataAccess
dotnet ef database update --project AiStockTradeApp.DataAccess
```

#### Connection Issues
- Verify connection string format
- Check SQL Server service status
- Validate authentication credentials
- Test network connectivity

#### Performance Issues
- Review query execution plans
- Add appropriate indexes
- Consider query optimization
- Monitor connection pool usage

## Dependencies

This project references:

- **AiStockTradeApp.Entities** - Domain models and entities
- **Microsoft.EntityFrameworkCore** - ORM framework
- **Microsoft.EntityFrameworkCore.SqlServer** - SQL Server provider

## Related Projects

- **[AiStockTradeApp.Api](../AiStockTradeApp.Api/)** - API that uses this data access layer
- **[AiStockTradeApp.Services](../AiStockTradeApp.Services/)** - Services that consume repositories
- **[AiStockTradeApp.Entities](../AiStockTradeApp.Entities/)** - Entity models used by this layer
