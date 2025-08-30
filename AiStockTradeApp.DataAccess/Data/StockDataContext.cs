using Microsoft.EntityFrameworkCore;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Entities.Models;

namespace AiStockTradeApp.DataAccess
{
    public class StockDataContext : DbContext
    {
        public StockDataContext(DbContextOptions<StockDataContext> options) : base(options)
        {
        }

    public DbSet<StockData> StockData { get; set; }
    public DbSet<ListedStock> ListedStocks { get; set; }
    public DbSet<HistoricalPrice> HistoricalPrices { get; set; }
    public DbSet<UserWatchlistItem> UserWatchlistItems { get; set; }
    public DbSet<UserPriceAlert> UserPriceAlerts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure StockData entity
            modelBuilder.Entity<StockData>(entity =>
            {
                // Primary key
                entity.HasKey(e => e.Id);

                // Indexes for better performance
                entity.HasIndex(e => e.Symbol)
                    .IsUnique(false); // Allow multiple entries for same symbol (historical data)

                entity.HasIndex(e => new { e.Symbol, e.CachedAt })
                    .HasDatabaseName("IX_StockData_Symbol_CachedAt");

                // Configure decimal precision
                entity.Property(e => e.Price)
                    .HasPrecision(18, 4);

                entity.Property(e => e.Change)
                    .HasPrecision(18, 4);

                // Configure string lengths
                entity.Property(e => e.Symbol)
                    .HasMaxLength(10)
                    .IsRequired();

                entity.Property(e => e.Currency)
                    .HasMaxLength(3)
                    .HasDefaultValue("USD");

                entity.Property(e => e.CompanyName)
                    .HasMaxLength(500);

                entity.Property(e => e.PercentChange)
                    .HasMaxLength(20);

                // Configure large text fields
                entity.Property(e => e.AIAnalysis)
                    .HasColumnType("nvarchar(max)");

                entity.Property(e => e.Recommendation)
                    .HasMaxLength(50);

                entity.Property(e => e.RecommendationReason)
                    .HasColumnType("nvarchar(max)");

                entity.Property(e => e.ChartDataJson)
                    .HasColumnType("nvarchar(max)");

                // Configure dates
                entity.Property(e => e.LastUpdated)
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(e => e.CachedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                // Configure cache duration as ticks (long)
                entity.Property(e => e.CacheDuration)
                    .HasConversion(
                        v => v.Ticks,
                        v => new TimeSpan(v))
                    .HasDefaultValue(TimeSpan.FromMinutes(15));
            });

            // Configure ListedStock entity
            modelBuilder.Entity<ListedStock>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Symbol).IsUnique();
                entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(500).IsRequired();
                entity.Property(e => e.Sector).HasMaxLength(100);
                entity.Property(e => e.Industry).HasMaxLength(200);
                // Helpful filter indexes
                entity.HasIndex(e => e.Sector);
                entity.HasIndex(e => e.Industry);
            });

            // Configure HistoricalPrice entity
            modelBuilder.Entity<HistoricalPrice>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
                entity.HasIndex(e => new { e.Symbol, e.Date }).IsUnique();
                entity.Property(e => e.Open).HasPrecision(18, 4);
                entity.Property(e => e.High).HasPrecision(18, 4);
                entity.Property(e => e.Low).HasPrecision(18, 4);
                entity.Property(e => e.Close).HasPrecision(18, 4);
            });

            // Configure UserWatchlistItem entity
            modelBuilder.Entity<UserWatchlistItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                // Composite index for user + symbol (unique)
                entity.HasIndex(e => new { e.UserId, e.Symbol })
                    .IsUnique()
                    .HasDatabaseName("IX_UserWatchlistItem_UserId_Symbol");
                
                // Index for user (for querying user's watchlist)
                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("IX_UserWatchlistItem_UserId");

                // Configure properties
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
                entity.Property(e => e.Alias).HasMaxLength(100);
                entity.Property(e => e.TargetPrice).HasPrecision(18, 2);
                entity.Property(e => e.StopLossPrice).HasPrecision(18, 2);
                
                entity.Property(e => e.AddedAt)
                    .HasDefaultValueSql("GETUTCDATE()");
            });

            // Configure UserPriceAlert entity
            modelBuilder.Entity<UserPriceAlert>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                // Index for user + symbol (multiple alerts per symbol allowed)
                entity.HasIndex(e => new { e.UserId, e.Symbol })
                    .HasDatabaseName("IX_UserPriceAlert_UserId_Symbol");
                
                // Index for active alerts (for background processing)
                entity.HasIndex(e => e.IsActive)
                    .HasDatabaseName("IX_UserPriceAlert_IsActive");

                // Configure properties
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
                entity.Property(e => e.AlertType).HasMaxLength(20).IsRequired();
                entity.Property(e => e.TargetValue).HasPrecision(18, 2);
                entity.Property(e => e.Message).HasMaxLength(500);
                
                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}