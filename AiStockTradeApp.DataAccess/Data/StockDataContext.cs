using Microsoft.EntityFrameworkCore;
using AiStockTradeApp.Entities;

namespace AiStockTradeApp.DataAccess
{
    public class StockDataContext : DbContext
    {
        public StockDataContext(DbContextOptions<StockDataContext> options) : base(options)
        {
        }

    public DbSet<StockData> StockData { get; set; }
    public DbSet<ListedStock> ListedStocks { get; set; }

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
        }
    }
}