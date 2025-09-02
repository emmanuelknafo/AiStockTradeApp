# AiStockTradeApp.DataAccess - Entity Framework Data Layer

## 🚀 Project Overview

A .NET Entity Framework Core data access layer providing **database abstraction**, **repository pattern implementation**, and **data persistence** for the AI Stock Trade application. This project serves as the central data access foundation with comprehensive database management capabilities.

## 🏗️ Architecture Role

This project serves as the **data persistence layer** implementing clean architecture patterns:

- **Entity Framework Core** - ORM with code-first migrations
- **Repository Pattern** - Data access abstraction and testability
- **Unit of Work** - Transaction management and consistency
- **Database Context** - Centralized data access configuration
- **Migration Management** - Schema versioning and deployment
- **ASP.NET Identity Integration** - User management and authentication

### Key Responsibilities

- **Database schema management** through EF Core migrations
- **Data persistence operations** with optimized query patterns
- **Repository implementations** for domain entities
- **User identity management** with ASP.NET Core Identity
- **Database relationship configuration** and constraints
- **Performance optimization** through indexing and query optimization

## 📁 Project Structure

```
AiStockTradeApp.DataAccess/
├── Data/                           # Database Context & Configuration
│   ├── StockDataContext.cs         # Main EF Core DbContext with all entities
│   ├── SeedData/                   # Database seeding and initialization
│   │   ├── DefaultUsers.cs         # Default user accounts for development
│   │   ├── SampleStocks.cs         # Sample stock data for testing
│   │   └── MarketData.cs           # Initial market and exchange data
│   └── Configurations/             # Entity configurations and relationships
│       ├── StockDataConfiguration.cs     # StockData entity configuration
│       ├── HistoricalPriceConfiguration.cs # Historical price indexing
│       ├── UserConfiguration.cs          # User entity extensions
│       └── WatchlistConfiguration.cs     # Watchlist relationship setup
├── Migrations/                     # EF Core Database Migrations
│   ├── 20241201_InitialCreate.cs   # Initial database schema
│   ├── 20241205_AddUserWatchlist.cs # User watchlist functionality
│   ├── 20241210_AddPortfolio.cs    # Portfolio management features
│   ├── 20241215_AddPriceAlerts.cs  # Price alert system
│   └── (Additional migrations...)  # Schema evolution tracking
├── Interfaces/                     # Repository Contracts
│   ├── IRepository.cs              # Generic repository interface
│   ├── IUnitOfWork.cs              # Unit of work pattern interface
│   ├── IStockDataRepository.cs     # Stock data operations contract
│   ├── IHistoricalPriceRepository.cs # Historical data operations
│   ├── IListedStockRepository.cs   # Listed stock management
│   ├── IUserWatchlistRepository.cs # User watchlist operations
│   ├── IUserPortfolioRepository.cs # Portfolio management operations
│   └── IPriceAlertRepository.cs    # Price alert management
├── Repositories/                   # Repository Implementations
│   ├── Repository.cs               # Generic repository base implementation
│   ├── UnitOfWork.cs               # Unit of work implementation
│   ├── StockDataRepository.cs      # Stock data persistence and queries
│   ├── HistoricalPriceRepository.cs # Historical data with time-series optimization
│   ├── ListedStockRepository.cs    # Listed stock CRUD operations
│   ├── UserWatchlistRepository.cs  # User watchlist management
│   ├── UserPortfolioRepository.cs  # Portfolio tracking and calculations
│   └── PriceAlertRepository.cs     # Price alert monitoring and triggers
├── DesignTime/                     # Development & Migration Support
│   ├── StockDataContextFactory.cs # Design-time DbContext factory
│   └── MigrationHelpers.cs        # Migration utility methods
└── AiStockTradeApp.DataAccess.csproj # Project configuration
```

## 🔧 Technology Stack

### Core Framework
- **.NET 9** - Latest LTS framework
- **Entity Framework Core 9** - ORM with advanced features
- **Microsoft.AspNetCore.Identity.EntityFrameworkCore** - User management
- **SQL Server Provider** - Primary database provider

### Database Features
- **Code-First Migrations** - Schema version control
- **Fluent API Configuration** - Advanced entity relationships
- **Query Optimization** - Indexed queries and performance tuning
- **Connection Resiliency** - Automatic retry policies
- **Database Seeding** - Development and test data initialization

## 🗄️ Database Context

### StockDataContext
Main Entity Framework DbContext managing all application entities:

```csharp
public class StockDataContext : IdentityDbContext<ApplicationUser>
{
    public StockDataContext(DbContextOptions<StockDataContext> options) : base(options) { }

    // Core Stock Data
    public DbSet<StockData> StockData { get; set; }
    public DbSet<HistoricalPrice> HistoricalPrices { get; set; }
    public DbSet<ListedStock> ListedStocks { get; set; }

    // User-Related Data
    public DbSet<UserWatchlistItem> UserWatchlistItems { get; set; }
    public DbSet<UserPortfolioItem> UserPortfolioItems { get; set; }
    public DbSet<PriceAlert> PriceAlerts { get; set; }
    public DbSet<UserPreferences> UserPreferences { get; set; }

    // Session Data
    public DbSet<WatchlistItem> WatchlistItems { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all entity configurations
        builder.ApplyConfigurationsFromAssembly(typeof(StockDataContext).Assembly);

        // Custom configurations
        ConfigureStockData(builder);
        ConfigureHistoricalPrices(builder);
        ConfigureUserRelationships(builder);
        ConfigureIndexes(builder);
    }

    private static void ConfigureStockData(ModelBuilder builder)
    {
        builder.Entity<StockData>(entity =>
        {
            entity.HasKey(e => e.Symbol);
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CurrentPrice).HasPrecision(18, 4);
            entity.Property(e => e.Change).HasPrecision(18, 4);
            entity.Property(e => e.ChangePercent).HasPrecision(18, 4);
            entity.Property(e => e.DataSource).HasMaxLength(50).IsRequired();
            
            // Indexes for performance
            entity.HasIndex(e => e.LastUpdated);
            entity.HasIndex(e => e.CompanyName);
        });
    }

    private static void ConfigureHistoricalPrices(ModelBuilder builder)
    {
        builder.Entity<HistoricalPrice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Open).HasPrecision(18, 4);
            entity.Property(e => e.High).HasPrecision(18, 4);
            entity.Property(e => e.Low).HasPrecision(18, 4);
            entity.Property(e => e.Close).HasPrecision(18, 4);
            entity.Property(e => e.AdjustedClose).HasPrecision(18, 4);

            // Critical index for time-series queries
            entity.HasIndex(e => new { e.Symbol, e.Date }).IsUnique();
            entity.HasIndex(e => e.Date);

            // Foreign key relationship
            entity.HasOne<StockData>()
                .WithMany()
                .HasForeignKey(e => e.Symbol)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureUserRelationships(ModelBuilder builder)
    {
        // User Watchlist Configuration
        builder.Entity<UserWatchlistItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.UserAlias).HasMaxLength(100);

            entity.HasIndex(e => new { e.UserId, e.Symbol }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.SortOrder });

            entity.HasOne(e => e.User)
                .WithMany(u => u.WatchlistItems)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Stock)
                .WithMany()
                .HasForeignKey(e => e.Symbol)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // User Portfolio Configuration
        builder.Entity<UserPortfolioItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 8);
            entity.Property(e => e.PurchasePrice).HasPrecision(18, 4);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasIndex(e => new { e.UserId, e.Symbol });
            entity.HasIndex(e => e.PurchaseDate);

            entity.HasOne(e => e.User)
                .WithMany(u => u.PortfolioItems)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Stock)
                .WithMany()
                .HasForeignKey(e => e.Symbol)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Price Alert Configuration
        builder.Entity<PriceAlert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.TargetPrice).HasPrecision(18, 4);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasIndex(e => new { e.UserId, e.IsActive });
            entity.HasIndex(e => new { e.Symbol, e.IsActive });

            entity.HasOne(e => e.User)
                .WithMany(u => u.PriceAlerts)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureIndexes(ModelBuilder builder)
    {
        // Performance optimization indexes
        builder.Entity<ListedStock>(entity =>
        {
            entity.HasIndex(e => e.Exchange);
            entity.HasIndex(e => e.Sector);
            entity.HasIndex(e => e.Industry);
            entity.HasIndex(e => e.IsActive);
        });

        // Session watchlist indexes
        builder.Entity<WatchlistItem>(entity =>
        {
            entity.HasKey(e => new { e.SessionId, e.Symbol });
            entity.HasIndex(e => e.AddedAt);
        });
    }
}
```

## 🔧 Repository Pattern Implementation

### Generic Repository Interface

```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);
    void Update(T entity);
    void UpdateRange(IEnumerable<T> entities);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
}
```

### Generic Repository Implementation

```csharp
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly StockDataContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(StockDataContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(object id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate);
    }

    public virtual async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities)
    {
        await _dbSet.AddRangeAsync(entities);
    }

    public virtual void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public virtual void UpdateRange(IEnumerable<T> entities)
    {
        _dbSet.UpdateRange(entities);
    }

    public virtual void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }

    public virtual void RemoveRange(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        return predicate == null 
            ? await _dbSet.CountAsync()
            : await _dbSet.CountAsync(predicate);
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }
}
```

## 📈 Specialized Repository Interfaces

### Stock Data Repository

```csharp
public interface IStockDataRepository : IRepository<StockData>
{
    Task<StockData?> GetBySymbolAsync(string symbol);
    Task<IEnumerable<StockData>> GetBySymbolsAsync(IEnumerable<string> symbols);
    Task<IEnumerable<StockData>> GetRecentlyUpdatedAsync(DateTime since);
    Task<IEnumerable<StockData>> GetTopGainersAsync(int count = 10);
    Task<IEnumerable<StockData>> GetTopLosersAsync(int count = 10);
    Task<IEnumerable<StockData>> GetMostActiveAsync(int count = 10);
    Task<IEnumerable<StockData>> SearchByCompanyNameAsync(string searchTerm);
    Task UpsertAsync(StockData stockData);
    Task UpsertRangeAsync(IEnumerable<StockData> stockDataList);
}
```

### Historical Price Repository

```csharp
public interface IHistoricalPriceRepository : IRepository<HistoricalPrice>
{
    Task<IEnumerable<HistoricalPrice>> GetBySymbolAsync(string symbol, int daysBack = 365);
    Task<IEnumerable<HistoricalPrice>> GetBySymbolDateRangeAsync(string symbol, DateTime startDate, DateTime endDate);
    Task<HistoricalPrice?> GetLatestBySymbolAsync(string symbol);
    Task<IEnumerable<HistoricalPrice>> GetForDateAsync(DateTime date);
    Task<decimal?> GetAveragePriceAsync(string symbol, int daysBack = 30);
    Task<decimal?> GetVolatilityAsync(string symbol, int daysBack = 30);
    Task<IEnumerable<HistoricalPrice>> GetHighVolumePeriodsAsync(string symbol, long minVolume);
    Task BulkInsertAsync(IEnumerable<HistoricalPrice> prices);
    Task DeleteOldDataAsync(DateTime cutoffDate);
}
```

### User Watchlist Repository

```csharp
public interface IUserWatchlistRepository : IRepository<UserWatchlistItem>
{
    Task<IEnumerable<UserWatchlistItem>> GetUserWatchlistAsync(string userId);
    Task<UserWatchlistItem?> GetUserWatchlistItemAsync(string userId, string symbol);
    Task<bool> IsInUserWatchlistAsync(string userId, string symbol);
    Task AddToWatchlistAsync(string userId, string symbol, string? userAlias = null);
    Task RemoveFromWatchlistAsync(string userId, string symbol);
    Task UpdateSortOrderAsync(string userId, List<(string Symbol, int SortOrder)> sortOrders);
    Task<int> GetWatchlistCountAsync(string userId);
}
```

## 🔄 Repository Implementations

### Stock Data Repository Implementation

```csharp
public class StockDataRepository : Repository<StockData>, IStockDataRepository
{
    public StockDataRepository(StockDataContext context) : base(context) { }

    public async Task<StockData?> GetBySymbolAsync(string symbol)
    {
        return await _dbSet.FirstOrDefaultAsync(s => s.Symbol == symbol.ToUpper());
    }

    public async Task<IEnumerable<StockData>> GetBySymbolsAsync(IEnumerable<string> symbols)
    {
        var upperSymbols = symbols.Select(s => s.ToUpper()).ToList();
        return await _dbSet
            .Where(s => upperSymbols.Contains(s.Symbol))
            .OrderBy(s => s.Symbol)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockData>> GetRecentlyUpdatedAsync(DateTime since)
    {
        return await _dbSet
            .Where(s => s.LastUpdated >= since)
            .OrderByDescending(s => s.LastUpdated)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockData>> GetTopGainersAsync(int count = 10)
    {
        return await _dbSet
            .Where(s => s.ChangePercent > 0)
            .OrderByDescending(s => s.ChangePercent)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockData>> GetTopLosersAsync(int count = 10)
    {
        return await _dbSet
            .Where(s => s.ChangePercent < 0)
            .OrderBy(s => s.ChangePercent)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockData>> GetMostActiveAsync(int count = 10)
    {
        return await _dbSet
            .Where(s => s.Volume > 0)
            .OrderByDescending(s => s.Volume)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockData>> SearchByCompanyNameAsync(string searchTerm)
    {
        var term = searchTerm.ToLower();
        return await _dbSet
            .Where(s => s.CompanyName.ToLower().Contains(term) || 
                       s.Symbol.ToLower().Contains(term))
            .OrderBy(s => s.Symbol)
            .ToListAsync();
    }

    public async Task UpsertAsync(StockData stockData)
    {
        var existing = await GetBySymbolAsync(stockData.Symbol);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(stockData);
        }
        else
        {
            await AddAsync(stockData);
        }
    }

    public async Task UpsertRangeAsync(IEnumerable<StockData> stockDataList)
    {
        foreach (var stockData in stockDataList)
        {
            await UpsertAsync(stockData);
        }
    }
}
```

### Historical Price Repository Implementation

```csharp
public class HistoricalPriceRepository : Repository<HistoricalPrice>, IHistoricalPriceRepository
{
    public HistoricalPriceRepository(StockDataContext context) : base(context) { }

    public async Task<IEnumerable<HistoricalPrice>> GetBySymbolAsync(string symbol, int daysBack = 365)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
        return await _dbSet
            .Where(h => h.Symbol == symbol.ToUpper() && h.Date >= cutoffDate)
            .OrderBy(h => h.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<HistoricalPrice>> GetBySymbolDateRangeAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(h => h.Symbol == symbol.ToUpper() && 
                       h.Date >= startDate && 
                       h.Date <= endDate)
            .OrderBy(h => h.Date)
            .ToListAsync();
    }

    public async Task<HistoricalPrice?> GetLatestBySymbolAsync(string symbol)
    {
        return await _dbSet
            .Where(h => h.Symbol == symbol.ToUpper())
            .OrderByDescending(h => h.Date)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<HistoricalPrice>> GetForDateAsync(DateTime date)
    {
        return await _dbSet
            .Where(h => h.Date.Date == date.Date)
            .OrderBy(h => h.Symbol)
            .ToListAsync();
    }

    public async Task<decimal?> GetAveragePriceAsync(string symbol, int daysBack = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
        return await _dbSet
            .Where(h => h.Symbol == symbol.ToUpper() && h.Date >= cutoffDate)
            .AverageAsync(h => (decimal?)h.Close);
    }

    public async Task<decimal?> GetVolatilityAsync(string symbol, int daysBack = 30)
    {
        var prices = await GetBySymbolAsync(symbol, daysBack);
        if (!prices.Any()) return null;

        var returns = new List<decimal>();
        var priceList = prices.OrderBy(p => p.Date).ToList();
        
        for (int i = 1; i < priceList.Count; i++)
        {
            var dailyReturn = (priceList[i].Close - priceList[i - 1].Close) / priceList[i - 1].Close;
            returns.Add(dailyReturn);
        }

        if (!returns.Any()) return null;

        var mean = returns.Average();
        var variance = returns.Sum(r => Math.Pow((double)(r - mean), 2)) / returns.Count;
        return (decimal)Math.Sqrt(variance);
    }

    public async Task<IEnumerable<HistoricalPrice>> GetHighVolumePeriodsAsync(string symbol, long minVolume)
    {
        return await _dbSet
            .Where(h => h.Symbol == symbol.ToUpper() && h.Volume >= minVolume)
            .OrderByDescending(h => h.Volume)
            .ToListAsync();
    }

    public async Task BulkInsertAsync(IEnumerable<HistoricalPrice> prices)
    {
        await _dbSet.AddRangeAsync(prices);
    }

    public async Task DeleteOldDataAsync(DateTime cutoffDate)
    {
        var oldData = await _dbSet
            .Where(h => h.Date < cutoffDate)
            .ToListAsync();
        
        if (oldData.Any())
        {
            _dbSet.RemoveRange(oldData);
        }
    }
}
```

## 🔄 Unit of Work Pattern

### Unit of Work Interface

```csharp
public interface IUnitOfWork : IDisposable
{
    IStockDataRepository StockData { get; }
    IHistoricalPriceRepository HistoricalPrices { get; }
    IListedStockRepository ListedStocks { get; }
    IUserWatchlistRepository UserWatchlists { get; }
    IUserPortfolioRepository UserPortfolios { get; }
    IPriceAlertRepository PriceAlerts { get; }

    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
```

### Unit of Work Implementation

```csharp
public class UnitOfWork : IUnitOfWork
{
    private readonly StockDataContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(StockDataContext context)
    {
        _context = context;
        StockData = new StockDataRepository(_context);
        HistoricalPrices = new HistoricalPriceRepository(_context);
        ListedStocks = new ListedStockRepository(_context);
        UserWatchlists = new UserWatchlistRepository(_context);
        UserPortfolios = new UserPortfolioRepository(_context);
        PriceAlerts = new PriceAlertRepository(_context);
    }

    public IStockDataRepository StockData { get; }
    public IHistoricalPriceRepository HistoricalPrices { get; }
    public IListedStockRepository ListedStocks { get; }
    public IUserWatchlistRepository UserWatchlists { get; }
    public IUserPortfolioRepository UserPortfolios { get; }
    public IPriceAlertRepository PriceAlerts { get; }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
```

## 🗃️ Database Migrations

### Migration Management

```bash
# Add a new migration
dotnet ef migrations add MigrationName --project AiStockTradeApp.DataAccess

# Update database to latest migration
dotnet ef database update --project AiStockTradeApp.DataAccess

# Generate SQL script for deployment
dotnet ef migrations script --project AiStockTradeApp.DataAccess

# Remove last migration (if not applied)
dotnet ef migrations remove --project AiStockTradeApp.DataAccess

# List all migrations
dotnet ef migrations list --project AiStockTradeApp.DataAccess
```

### Design-Time DbContext Factory

```csharp
public class StockDataContextFactory : IDesignTimeDbContextFactory<StockDataContext>
{
    public StockDataContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<StockDataContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        optionsBuilder.UseSqlServer(connectionString, options =>
        {
            options.MigrationsAssembly(typeof(StockDataContext).Assembly.FullName);
            options.CommandTimeout(30);
        });

        // Enable sensitive data logging in development
        if (configuration.GetValue<bool>("Logging:EnableSensitiveDataLogging"))
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }

        return new StockDataContext(optionsBuilder.Options);
    }
}
```

## 🌱 Database Seeding

### Seed Data Implementation

```csharp
public static class SeedData
{
    public static async Task InitializeAsync(StockDataContext context, UserManager<ApplicationUser> userManager)
    {
        await SeedUsersAsync(userManager);
        await SeedListedStocksAsync(context);
        await SeedSampleStockDataAsync(context);
    }

    private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
    {
        // Create default admin user
        var adminEmail = "admin@aistocktradeapp.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "User",
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true,
                PreferredCulture = "en"
            };

            await userManager.CreateAsync(adminUser, "Admin123!");
        }
    }

    private static async Task SeedListedStocksAsync(StockDataContext context)
    {
        if (!await context.ListedStocks.AnyAsync())
        {
            var stocks = new List<ListedStock>
            {
                new() { Symbol = "AAPL", CompanyName = "Apple Inc.", Exchange = "NASDAQ", Sector = "Technology", Industry = "Consumer Electronics" },
                new() { Symbol = "MSFT", CompanyName = "Microsoft Corporation", Exchange = "NASDAQ", Sector = "Technology", Industry = "Software" },
                new() { Symbol = "GOOGL", CompanyName = "Alphabet Inc.", Exchange = "NASDAQ", Sector = "Technology", Industry = "Internet Services" },
                new() { Symbol = "AMZN", CompanyName = "Amazon.com Inc.", Exchange = "NASDAQ", Sector = "Consumer Discretionary", Industry = "E-commerce" },
                new() { Symbol = "TSLA", CompanyName = "Tesla Inc.", Exchange = "NASDAQ", Sector = "Consumer Discretionary", Industry = "Electric Vehicles" }
            };

            await context.ListedStocks.AddRangeAsync(stocks);
            await context.SaveChangesAsync();
        }
    }
}
```

## 🔧 Configuration & Dependencies

### Service Registration

```csharp
// Program.cs - Dependency injection setup
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        // DbContext registration
        services.AddDbContext<StockDataContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(StockDataContext).Assembly.FullName);
                sqlOptions.CommandTimeout(30);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
        });

        // Repository registrations
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IStockDataRepository, StockDataRepository>();
        services.AddScoped<IHistoricalPriceRepository, HistoricalPriceRepository>();
        services.AddScoped<IListedStockRepository, ListedStockRepository>();
        services.AddScoped<IUserWatchlistRepository, UserWatchlistRepository>();
        services.AddScoped<IUserPortfolioRepository, UserPortfolioRepository>();
        services.AddScoped<IPriceAlertRepository, PriceAlertRepository>();

        return services;
    }
}
```

### Project Dependencies

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\AiStockTradeApp.Entities\AiStockTradeApp.Entities.csproj" />
  </ItemGroup>
</Project>
```

## 🎯 Performance Optimization

### Query Optimization Patterns

```csharp
// Example: Optimized historical data query with projections
public async Task<IEnumerable<ChartDataPoint>> GetChartDataAsync(string symbol, int daysBack)
{
    var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
    
    return await _dbSet
        .Where(h => h.Symbol == symbol && h.Date >= cutoffDate)
        .Select(h => new ChartDataPoint
        {
            Date = h.Date,
            Value = h.Close,
            SecondaryValue = h.Volume
        })
        .OrderBy(h => h.Date)
        .ToListAsync();
}

// Example: Bulk operations for better performance
public async Task BulkUpdateStockDataAsync(IEnumerable<StockData> stockDataList)
{
    var symbols = stockDataList.Select(s => s.Symbol).ToList();
    var existingStocks = await _dbSet
        .Where(s => symbols.Contains(s.Symbol))
        .ToDictionaryAsync(s => s.Symbol);

    var toUpdate = new List<StockData>();
    var toAdd = new List<StockData>();

    foreach (var stockData in stockDataList)
    {
        if (existingStocks.TryGetValue(stockData.Symbol, out var existing))
        {
            _context.Entry(existing).CurrentValues.SetValues(stockData);
            toUpdate.Add(existing);
        }
        else
        {
            toAdd.Add(stockData);
        }
    }

    if (toAdd.Any())
    {
        await _dbSet.AddRangeAsync(toAdd);
    }
}
```

### Indexing Strategy

Key database indexes for optimal performance:

1. **StockData.Symbol** - Primary key, frequently queried
2. **StockData.LastUpdated** - Time-based filtering
3. **StockData.CompanyName** - Search functionality
4. **HistoricalPrice.Symbol + Date** - Unique constraint, time-series queries
5. **UserWatchlistItem.UserId + Symbol** - User-specific lookups
6. **PriceAlert.Symbol + IsActive** - Alert processing

## 🧪 Testing Support

### In-Memory Database for Testing

```csharp
public static class TestDbContextFactory
{
    public static StockDataContext CreateInMemoryContext(string databaseName = "TestDb")
    {
        var options = new DbContextOptionsBuilder<StockDataContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var context = new StockDataContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static async Task<StockDataContext> CreateSeededContextAsync(string databaseName = "TestDb")
    {
        var context = CreateInMemoryContext(databaseName);
        await SeedTestDataAsync(context);
        return context;
    }

    private static async Task SeedTestDataAsync(StockDataContext context)
    {
        var testStocks = new List<StockData>
        {
            new() { Symbol = "TEST1", CompanyName = "Test Company 1", CurrentPrice = 100.00m, DataSource = "Test" },
            new() { Symbol = "TEST2", CompanyName = "Test Company 2", CurrentPrice = 200.00m, DataSource = "Test" }
        };

        await context.StockData.AddRangeAsync(testStocks);
        await context.SaveChangesAsync();
    }
}
```

## 🔧 Development Guidelines

### Repository Development Patterns

1. **Always use async methods** for database operations
2. **Implement proper error handling** with try-catch blocks
3. **Use projections** for read-only operations to improve performance
4. **Implement bulk operations** for large data sets
5. **Add appropriate indexes** for new query patterns
6. **Write unit tests** for all repository methods
7. **Use transactions** for multi-table operations

### Migration Best Practices

1. **Always review generated migrations** before applying
2. **Use descriptive migration names** that explain the change
3. **Test migrations** on a copy of production data
4. **Backup database** before applying migrations to production
5. **Use `dotnet ef migrations script`** for deployment automation

This comprehensive data access layer provides a robust foundation for all database operations in the AI Stock Trade application, emphasizing performance, maintainability, and testability through proven patterns and practices.
