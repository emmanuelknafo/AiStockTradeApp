using ai_stock_trade_app.Models;

namespace ai_stock_trade_app.Tests.Models
{
    public class StockDataTests
    {
        [Fact]
        public void StockData_DefaultValues_AreCorrect()
        {
            // Act
            var stockData = new StockData();

            // Assert
            Assert.Equal(string.Empty, stockData.Symbol);
            Assert.Equal(0m, stockData.Price);
            Assert.Equal(0m, stockData.Change);
            Assert.Equal(string.Empty, stockData.PercentChange);
            Assert.True(stockData.LastUpdated <= DateTime.UtcNow);
            Assert.Equal(string.Empty, stockData.CompanyName);
        }

        [Fact]
        public void StockData_SetProperties_WorksCorrectly()
        {
            // Arrange & Act
            var stockData = new StockData
            {
                Symbol = "AAPL",
                Price = 150.50m,
                Change = 2.30m,
                PercentChange = "1.55%",
                CompanyName = "Apple Inc.",
                AIAnalysis = "Strong buy signal",
                Recommendation = "Buy"
            };

            // Assert
            Assert.Equal("AAPL", stockData.Symbol);
            Assert.Equal(150.50m, stockData.Price);
            Assert.Equal(2.30m, stockData.Change);
            Assert.Equal("1.55%", stockData.PercentChange);
            Assert.Equal("Apple Inc.", stockData.CompanyName);
            Assert.Equal("Strong buy signal", stockData.AIAnalysis);
            Assert.Equal("Buy", stockData.Recommendation);
        }
    }

    public class ChartDataPointTests
    {
        [Fact]
        public void ChartDataPoint_DefaultValues_AreCorrect()
        {
            // Act
            var chartPoint = new ChartDataPoint();

            // Assert
            Assert.Equal(DateTime.MinValue, chartPoint.Date);
            Assert.Equal(0m, chartPoint.Price);
            Assert.Equal(0m, chartPoint.Volume);
        }

        [Fact]
        public void ChartDataPoint_SetProperties_WorksCorrectly()
        {
            // Arrange
            var date = DateTime.Today;
            var price = 125.75m;
            var volume = 1500000m;

            // Act
            var chartPoint = new ChartDataPoint
            {
                Date = date,
                Price = price,
                Volume = volume
            };

            // Assert
            Assert.Equal(date, chartPoint.Date);
            Assert.Equal(price, chartPoint.Price);
            Assert.Equal(volume, chartPoint.Volume);
        }
    }

    public class WatchlistItemTests
    {
        [Fact]
        public void WatchlistItem_DefaultValues_AreCorrect()
        {
            // Act
            var item = new WatchlistItem();

            // Assert
            Assert.Equal(string.Empty, item.Symbol);
            Assert.True(item.AddedDate <= DateTime.UtcNow);
            Assert.Null(item.StockData);
        }

        [Fact]
        public void WatchlistItem_WithStockData_WorksCorrectly()
        {
            // Arrange
            var stockData = new StockData
            {
                Symbol = "MSFT",
                Price = 300.00m,
                Change = -1.50m
            };

            // Act
            var item = new WatchlistItem
            {
                Symbol = "MSFT",
                StockData = stockData
            };

            // Assert
            Assert.Equal("MSFT", item.Symbol);
            Assert.NotNull(item.StockData);
            Assert.Equal(stockData.Symbol, item.StockData.Symbol);
            Assert.Equal(stockData.Price, item.StockData.Price);
        }
    }

    public class PortfolioSummaryTests
    {
        [Fact]
        public void PortfolioSummary_DefaultValues_AreCorrect()
        {
            // Act
            var portfolio = new PortfolioSummary();

            // Assert
            Assert.Equal(0m, portfolio.TotalValue);
            Assert.Equal(0m, portfolio.TotalChange);
            Assert.Equal(0m, portfolio.TotalChangePercent);
            Assert.Equal(0, portfolio.StockCount);
        }

        [Fact]
        public void PortfolioSummary_SetProperties_WorksCorrectly()
        {
            // Act
            var portfolio = new PortfolioSummary
            {
                TotalValue = 10000.00m,
                TotalChange = 250.50m,
                TotalChangePercent = 2.57m,
                StockCount = 5
            };

            // Assert
            Assert.Equal(10000.00m, portfolio.TotalValue);
            Assert.Equal(250.50m, portfolio.TotalChange);
            Assert.Equal(2.57m, portfolio.TotalChangePercent);
            Assert.Equal(5, portfolio.StockCount);
        }
    }

    public class PriceAlertTests
    {
        [Fact]
        public void PriceAlert_DefaultValues_AreCorrect()
        {
            // Act
            var alert = new PriceAlert();

            // Assert
            Assert.Equal(string.Empty, alert.Symbol);
            Assert.Equal(0m, alert.TargetPrice);
            Assert.Equal(string.Empty, alert.AlertType);
            Assert.True(alert.CreatedDate <= DateTime.UtcNow);
            Assert.False(alert.IsTriggered);
        }

        [Fact]
        public void PriceAlert_SetProperties_WorksCorrectly()
        {
            // Arrange
            var createdDate = DateTime.UtcNow.AddDays(-1);

            // Act
            var alert = new PriceAlert
            {
                Symbol = "TSLA",
                TargetPrice = 250.00m,
                AlertType = "above",
                CreatedDate = createdDate,
                IsTriggered = true
            };

            // Assert
            Assert.Equal("TSLA", alert.Symbol);
            Assert.Equal(250.00m, alert.TargetPrice);
            Assert.Equal("above", alert.AlertType);
            Assert.Equal(createdDate, alert.CreatedDate);
            Assert.True(alert.IsTriggered);
        }
    }
}
