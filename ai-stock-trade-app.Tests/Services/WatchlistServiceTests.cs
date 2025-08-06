using ai_stock_trade_app.Models;
using ai_stock_trade_app.Services;
using Microsoft.Extensions.Logging;

namespace ai_stock_trade_app.Tests.Services
{
    public class WatchlistServiceTests
    {
        private readonly Mock<ILogger<WatchlistService>> _mockLogger;
        private readonly WatchlistService _watchlistService;

        public WatchlistServiceTests()
        {
            _mockLogger = new Mock<ILogger<WatchlistService>>();
            _watchlistService = new WatchlistService(_mockLogger.Object);
        }

        [Fact]
        public async Task GetWatchlistAsync_NewSession_ShouldReturnEmptyList()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();

            // Act
            var result = await _watchlistService.GetWatchlistAsync(sessionId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task AddToWatchlistAsync_ValidSymbol_ShouldAddSuccessfully()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var symbol = "AAPL";

            // Act
            await _watchlistService.AddToWatchlistAsync(sessionId, symbol);
            var watchlist = await _watchlistService.GetWatchlistAsync(sessionId);

            // Assert
            watchlist.Should().HaveCount(1);
            watchlist.First().Symbol.Should().Be("AAPL");
            watchlist.First().AddedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task AddToWatchlistAsync_DuplicateSymbol_ShouldNotAddDuplicate()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var symbol = "AAPL";

            // Act
            await _watchlistService.AddToWatchlistAsync(sessionId, symbol);
            await _watchlistService.AddToWatchlistAsync(sessionId, symbol);
            var watchlist = await _watchlistService.GetWatchlistAsync(sessionId);

            // Assert
            watchlist.Should().HaveCount(1);
            watchlist.First().Symbol.Should().Be("AAPL");
        }

        [Fact]
        public async Task RemoveFromWatchlistAsync_ExistingSymbol_ShouldRemoveSuccessfully()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var symbol = "AAPL";
            await _watchlistService.AddToWatchlistAsync(sessionId, symbol);

            // Act
            await _watchlistService.RemoveFromWatchlistAsync(sessionId, symbol);
            var watchlist = await _watchlistService.GetWatchlistAsync(sessionId);

            // Assert
            watchlist.Should().BeEmpty();
        }

        [Fact]
        public async Task RemoveFromWatchlistAsync_NonExistentSymbol_ShouldNotThrow()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();

            // Act & Assert
            var act = async () => await _watchlistService.RemoveFromWatchlistAsync(sessionId, "NONEXISTENT");
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task ClearWatchlistAsync_WithItems_ShouldRemoveAllItems()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var symbols = new[] { "AAPL", "GOOGL", "MSFT" };

            foreach (var symbol in symbols)
            {
                await _watchlistService.AddToWatchlistAsync(sessionId, symbol);
            }

            // Act
            await _watchlistService.ClearWatchlistAsync(sessionId);
            var watchlist = await _watchlistService.GetWatchlistAsync(sessionId);

            // Assert
            watchlist.Should().BeEmpty();
        }

        [Fact]
        public async Task GetWatchlistAsync_DifferentSessions_ShouldBeIsolated()
        {
            // Arrange
            var sessionId1 = Guid.NewGuid().ToString();
            var sessionId2 = Guid.NewGuid().ToString();

            // Act
            await _watchlistService.AddToWatchlistAsync(sessionId1, "AAPL");
            await _watchlistService.AddToWatchlistAsync(sessionId2, "GOOGL");

            var watchlist1 = await _watchlistService.GetWatchlistAsync(sessionId1);
            var watchlist2 = await _watchlistService.GetWatchlistAsync(sessionId2);

            // Assert
            watchlist1.Should().HaveCount(1);
            watchlist1.First().Symbol.Should().Be("AAPL");

            watchlist2.Should().HaveCount(1);
            watchlist2.First().Symbol.Should().Be("GOOGL");
        }

        [Fact]
        public async Task CalculatePortfolioSummaryAsync_EmptyWatchlist_ShouldReturnZeroValues()
        {
            // Arrange
            var emptyWatchlist = new List<WatchlistItem>();

            // Act
            var portfolio = await _watchlistService.CalculatePortfolioSummaryAsync(emptyWatchlist);

            // Assert
            portfolio.Should().NotBeNull();
            portfolio.TotalValue.Should().Be(0);
            portfolio.TotalChange.Should().Be(0);
            portfolio.StockCount.Should().Be(0);
        }

        [Fact]
        public async Task CalculatePortfolioSummaryAsync_WithStockData_ShouldCalculateCorrectly()
        {
            // Arrange
            var watchlist = new List<WatchlistItem>
            {
                new WatchlistItem 
                { 
                    Symbol = "AAPL", 
                    StockData = new StockData { Symbol = "AAPL", Price = 150m, Change = 5m }
                },
                new WatchlistItem 
                { 
                    Symbol = "GOOGL", 
                    StockData = new StockData { Symbol = "GOOGL", Price = 2500m, Change = -10m }
                }
            };

            // Act
            var portfolio = await _watchlistService.CalculatePortfolioSummaryAsync(watchlist);

            // Assert
            portfolio.Should().NotBeNull();
            portfolio.TotalValue.Should().Be(2650m); // 150 + 2500
            portfolio.TotalChange.Should().Be(-5m); // 5 + (-10)
            portfolio.StockCount.Should().Be(2);
        }

        [Fact]
        public async Task GetAlertsAsync_NewSession_ShouldReturnEmptyList()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();

            // Act
            var result = await _watchlistService.GetAlertsAsync(sessionId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task AddAlertAsync_ValidAlert_ShouldAddSuccessfully()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var alert = new PriceAlert
            {
                Symbol = "AAPL",
                TargetPrice = 160m,
                AlertType = "above",
                CreatedDate = DateTime.UtcNow
            };

            // Act
            await _watchlistService.AddAlertAsync(sessionId, alert);
            var alerts = await _watchlistService.GetAlertsAsync(sessionId);

            // Assert
            alerts.Should().HaveCount(1);
            alerts.First().Symbol.Should().Be("AAPL");
            alerts.First().TargetPrice.Should().Be(160m);
        }

        [Fact]
        public async Task RemoveAlertAsync_ExistingAlert_ShouldRemoveSuccessfully()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var alert = new PriceAlert
            {
                Symbol = "AAPL",
                TargetPrice = 160m,
                AlertType = "above"
            };

            await _watchlistService.AddAlertAsync(sessionId, alert);

            // Act
            await _watchlistService.RemoveAlertAsync(sessionId, "AAPL", 160m);
            var alerts = await _watchlistService.GetAlertsAsync(sessionId);

            // Assert
            alerts.Should().BeEmpty();
        }

        [Fact]
        public async Task GetExportDataAsync_WithWatchlistData_ShouldReturnExportData()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            await _watchlistService.AddToWatchlistAsync(sessionId, "AAPL");

            // Act
            var exportData = await _watchlistService.GetExportDataAsync(sessionId);

            // Assert
            exportData.Should().NotBeNull();
            exportData.Watchlist.Should().HaveCount(1);
            exportData.ExportDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            exportData.Version.Should().Be("1.0");
        }

        [Fact]
        public async Task ImportDataAsync_ValidData_ShouldImportSuccessfully()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var importData = new ExportData
            {
                Watchlist = new List<WatchlistItem>
                {
                    new WatchlistItem { Symbol = "AAPL", AddedDate = DateTime.UtcNow },
                    new WatchlistItem { Symbol = "GOOGL", AddedDate = DateTime.UtcNow }
                }
            };

            // Act
            await _watchlistService.ImportDataAsync(sessionId, importData);
            var watchlist = await _watchlistService.GetWatchlistAsync(sessionId);

            // Assert
            watchlist.Should().HaveCount(2);
            watchlist.Should().Contain(w => w.Symbol == "AAPL");
            watchlist.Should().Contain(w => w.Symbol == "GOOGL");
        }
    }
}
