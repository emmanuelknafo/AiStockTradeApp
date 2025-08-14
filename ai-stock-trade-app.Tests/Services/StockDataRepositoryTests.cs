using AiStockTradeApp.DataAccess;
using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.DataAccess.Repositories;
using AiStockTradeApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ai_stock_trade_app.Tests.Services
{
    public class StockDataRepositoryTests : IDisposable
    {
        private readonly StockDataContext _context;
        private readonly Mock<ILogger<StockDataRepository>> _mockLogger;
        private readonly StockDataRepository _repository;

        public StockDataRepositoryTests()
        {
            // Use in-memory database for testing
            var options = new DbContextOptionsBuilder<StockDataContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new StockDataContext(options);
            _mockLogger = new Mock<ILogger<StockDataRepository>>();
            _repository = new StockDataRepository(_context, _mockLogger.Object);
        }

        [Fact]
        public async Task GetCachedStockDataAsync_ValidCachedData_ShouldReturnData()
        {
            // Arrange
            var stockData = new StockData
            {
                Symbol = "AAPL",
                Price = 150.50m,
                Change = 2.25m,
                PercentChange = "1.52%",
                CompanyName = "Apple Inc.",
                Currency = "USD",
                LastUpdated = DateTime.UtcNow,
                CachedAt = DateTime.UtcNow.AddMinutes(-5), // Cached 5 minutes ago
                CacheDuration = TimeSpan.FromMinutes(15) // Expires in 15 minutes
            };

            _context.StockData.Add(stockData);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetCachedStockDataAsync("AAPL");

            // Assert
            result.Should().NotBeNull();
            result!.Symbol.Should().Be("AAPL");
            result.Price.Should().Be(150.50m);
            result.IsCacheValid.Should().BeTrue();
        }

        [Fact]
        public async Task GetCachedStockDataAsync_ExpiredCache_ShouldReturnNull()
        {
            // Arrange
            var stockData = new StockData
            {
                Symbol = "AAPL",
                Price = 150.50m,
                Change = 2.25m,
                PercentChange = "1.52%",
                CompanyName = "Apple Inc.",
                Currency = "USD",
                LastUpdated = DateTime.UtcNow,
                CachedAt = DateTime.UtcNow.AddMinutes(-20), // Cached 20 minutes ago
                CacheDuration = TimeSpan.FromMinutes(15) // Expired 5 minutes ago
            };

            _context.StockData.Add(stockData);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetCachedStockDataAsync("AAPL");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetCachedStockDataAsync_NoData_ShouldReturnNull()
        {
            // Act
            var result = await _repository.GetCachedStockDataAsync("NONEXISTENT");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SaveStockDataAsync_ValidData_ShouldSaveAndReturnData()
        {
            // Arrange
            var stockData = new StockData
            {
                Symbol = "MSFT",
                Price = 300.75m,
                Change = -1.50m,
                PercentChange = "-0.50%",
                CompanyName = "Microsoft Corporation",
                Currency = "USD",
                LastUpdated = DateTime.UtcNow
            };

            // Act
            var result = await _repository.SaveStockDataAsync(stockData);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            result.CachedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            // Verify data was saved to database
            var savedData = await _context.StockData.FirstOrDefaultAsync(x => x.Symbol == "MSFT");
            savedData.Should().NotBeNull();
            savedData!.Price.Should().Be(300.75m);
        }

        [Fact]
        public async Task GetRecentStockDataAsync_MultipleEntries_ShouldReturnOrderedRecent()
        {
            // Arrange
            var stockData1 = new StockData
            {
                Symbol = "GOOGL",
                Price = 100.00m,
                Change = 1.00m,
                PercentChange = "1.00%",
                CompanyName = "Alphabet Inc.",
                Currency = "USD",
                LastUpdated = DateTime.UtcNow,
                CachedAt = DateTime.UtcNow.AddDays(-1)
            };

            var stockData2 = new StockData
            {
                Symbol = "GOOGL",
                Price = 102.00m,
                Change = 2.00m,
                PercentChange = "2.00%",
                CompanyName = "Alphabet Inc.",
                Currency = "USD",
                LastUpdated = DateTime.UtcNow,
                CachedAt = DateTime.UtcNow.AddHours(-1)
            };

            var stockData3 = new StockData
            {
                Symbol = "GOOGL",
                Price = 103.00m,
                Change = 3.00m,
                PercentChange = "3.00%",
                CompanyName = "Alphabet Inc.",
                Currency = "USD",
                LastUpdated = DateTime.UtcNow,
                CachedAt = DateTime.UtcNow.AddDays(-10) // Too old, should not be included
            };

            _context.StockData.AddRange(stockData1, stockData2, stockData3);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetRecentStockDataAsync("GOOGL", days: 7);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2); // Only the two recent ones
            result.Should().BeInDescendingOrder(x => x.CachedAt);
            result.First().Price.Should().Be(102.00m); // Most recent first
        }

        [Fact]
        public async Task CleanupExpiredCacheAsync_OldEntries_ShouldRemoveThem()
        {
            // Arrange
            var oldStockData = new StockData
            {
                Symbol = "OLD",
                Price = 50.00m,
                Change = 0.00m,
                PercentChange = "0.00%",
                CompanyName = "Old Company",
                Currency = "USD",
                LastUpdated = DateTime.UtcNow,
                CachedAt = DateTime.UtcNow.AddDays(-35) // Older than 30 days
            };

            var recentStockData = new StockData
            {
                Symbol = "NEW",
                Price = 150.00m,
                Change = 5.00m,
                PercentChange = "3.45%",
                CompanyName = "New Company",
                Currency = "USD",
                LastUpdated = DateTime.UtcNow,
                CachedAt = DateTime.UtcNow.AddDays(-5) // Recent
            };

            _context.StockData.AddRange(oldStockData, recentStockData);
            await _context.SaveChangesAsync();

            // Act
            await _repository.CleanupExpiredCacheAsync();

            // Assert
            var remainingData = await _context.StockData.ToListAsync();
            remainingData.Should().HaveCount(1);
            remainingData.First().Symbol.Should().Be("NEW");
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
