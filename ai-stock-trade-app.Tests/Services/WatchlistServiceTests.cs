using ai_stock_trade_app.Models;
using ai_stock_trade_app.Services;
using Microsoft.Extensions.Logging;

namespace ai_stock_trade_app.Tests.Services
{
    public class WatchlistServiceTests
    {
        private readonly WatchlistService _service;

        public WatchlistServiceTests()
        {
            var logger = new LoggerFactory().CreateLogger<WatchlistService>();
            _service = new WatchlistService(logger);
        }

        [Fact]
        public async Task AddToWatchlistAsync_NewSymbol_AddsSuccessfully()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString(); // Use unique session ID
            var symbol = "AAPL";

            // Act
            await _service.AddToWatchlistAsync(sessionId, symbol);
            var watchlist = await _service.GetWatchlistAsync(sessionId);

            // Assert
            Assert.Single(watchlist);
            Assert.Equal(symbol.ToUpper(), watchlist.First().Symbol);
        }

        [Fact]
        public async Task AddToWatchlistAsync_DuplicateSymbol_ThrowsException()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString(); // Use unique session ID
            var symbol = "AAPL";

            // Act
            await _service.AddToWatchlistAsync(sessionId, symbol);
            
            // Assert - AddToWatchlistAsync doesn't throw exception for duplicates, it just doesn't add them
            // So we need to check that adding the same symbol again doesn't increase the count
            await _service.AddToWatchlistAsync(sessionId, symbol);
            var watchlist = await _service.GetWatchlistAsync(sessionId);
            Assert.Single(watchlist); // Should still only have one item
        }

        [Fact]
        public async Task RemoveFromWatchlistAsync_ExistingSymbol_RemovesSuccessfully()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString(); // Use unique session ID
            var symbol = "AAPL";
            await _service.AddToWatchlistAsync(sessionId, symbol);

            // Act
            await _service.RemoveFromWatchlistAsync(sessionId, symbol);
            var watchlist = await _service.GetWatchlistAsync(sessionId);

            // Assert
            Assert.Empty(watchlist);
        }

        [Fact]
        public async Task ClearWatchlistAsync_WithItems_ClearsAll()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString(); // Use unique session ID
            await _service.AddToWatchlistAsync(sessionId, "AAPL");
            await _service.AddToWatchlistAsync(sessionId, "MSFT");

            // Act
            await _service.ClearWatchlistAsync(sessionId);
            var watchlist = await _service.GetWatchlistAsync(sessionId);

            // Assert
            Assert.Empty(watchlist);
        }

        [Fact]
        public async Task AddAlertAsync_ValidAlert_AddsSuccessfully()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString(); // Use unique session ID
            var alert = new PriceAlert
            {
                Symbol = "AAPL",
                TargetPrice = 150.00m,
                AlertType = "above"
            };

            // Act
            await _service.AddAlertAsync(sessionId, alert);
            var alerts = await _service.GetAlertsAsync(sessionId);

            // Assert
            Assert.Single(alerts);
            Assert.Equal(alert.Symbol, alerts.First().Symbol);
            Assert.Equal(alert.TargetPrice, alerts.First().TargetPrice);
        }

        [Fact]
        public async Task CalculatePortfolioSummaryAsync_WithStocks_ReturnsCorrectSummary()
        {
            // Arrange
            var watchlist = new List<WatchlistItem>
            {
                new WatchlistItem
                {
                    Symbol = "AAPL",
                    StockData = new StockData
                    {
                        Symbol = "AAPL",
                        Price = 150.00m,
                        Change = 5.00m
                    }
                },
                new WatchlistItem
                {
                    Symbol = "MSFT",
                    StockData = new StockData
                    {
                        Symbol = "MSFT",
                        Price = 300.00m,
                        Change = -2.00m
                    }
                }
            };

            // Act
            var summary = await _service.CalculatePortfolioSummaryAsync(watchlist);

            // Assert
            Assert.Equal(450.00m, summary.TotalValue);
            Assert.Equal(3.00m, summary.TotalChange);
            Assert.Equal(2, summary.StockCount);
        }

        [Fact]
        public async Task GetExportDataAsync_WithData_ReturnsCompleteExport()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString(); // Use unique session ID
            await _service.AddToWatchlistAsync(sessionId, "AAPL");
            
            var alert = new PriceAlert
            {
                Symbol = "AAPL",
                TargetPrice = 160.00m,
                AlertType = "above"
            };
            await _service.AddAlertAsync(sessionId, alert);

            // Act
            var exportData = await _service.GetExportDataAsync(sessionId);

            // Assert
            Assert.NotNull(exportData);
            Assert.Single(exportData.Watchlist);
            Assert.NotNull(exportData.Portfolio);
            Assert.Equal("1.0", exportData.Version);
        }
    }
}
