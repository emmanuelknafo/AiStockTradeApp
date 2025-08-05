using ai_stock_trade_app.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace ai_stock_trade_app.Tests.Utilities
{
    public static class TestDataHelper
    {
        public static StockData CreateTestStockData(
            string symbol = "AAPL",
            decimal price = 150.00m,
            decimal change = 2.50m,
            string percentChange = "1.69%")
        {
            return new StockData
            {
                Symbol = symbol,
                Price = price,
                Change = change,
                PercentChange = percentChange,
                LastUpdated = DateTime.UtcNow,
                CompanyName = $"{symbol} Inc.",
                AIAnalysis = $"Test analysis for {symbol}",
                Recommendation = "Buy"
            };
        }

        public static WatchlistItem CreateTestWatchlistItem(
            string symbol = "AAPL",
            StockData? stockData = null)
        {
            return new WatchlistItem
            {
                Symbol = symbol,
                AddedDate = DateTime.UtcNow,
                StockData = stockData ?? CreateTestStockData(symbol)
            };
        }

        public static PriceAlert CreateTestPriceAlert(
            string symbol = "AAPL",
            decimal targetPrice = 160.00m,
            string alertType = "above")
        {
            return new PriceAlert
            {
                Symbol = symbol,
                TargetPrice = targetPrice,
                AlertType = alertType,
                CreatedDate = DateTime.UtcNow,
                IsTriggered = false
            };
        }

        public static List<ChartDataPoint> CreateTestChartData(int days = 5)
        {
            var chartData = new List<ChartDataPoint>();
            var basePrice = 150.00m;
            var random = new Random(42); // Fixed seed for consistent tests

            for (int i = 0; i < days; i++)
            {
                var date = DateTime.Today.AddDays(-days + i + 1);
                var priceVariation = (decimal)(random.NextDouble() - 0.5) * 10; // ±5 price variation
                var price = Math.Max(basePrice + priceVariation, 1); // Ensure positive price
                var volume = random.Next(100000, 10000000);

                chartData.Add(new ChartDataPoint
                {
                    Date = date,
                    Price = Math.Round(price, 2),
                    Volume = volume
                });
            }

            return chartData;
        }

        public static ExportData CreateTestExportData()
        {
            var watchlist = new List<WatchlistItem>
            {
                CreateTestWatchlistItem("AAPL"),
                CreateTestWatchlistItem("MSFT", CreateTestStockData("MSFT", 300.00m, -1.50m, "-0.50%"))
            };

            var portfolio = new PortfolioSummary
            {
                TotalValue = 450.00m,
                TotalChange = 1.00m,
                TotalChangePercent = 0.22m,
                StockCount = 2
            };

            return new ExportData
            {
                Watchlist = watchlist,
                Portfolio = portfolio,
                ExportDate = DateTime.UtcNow,
                Version = "1.0"
            };
        }

        public static string CreateTestCsvContent()
        {
            return @"Ticker,Price,Change,Percent,Recommendation,Analysis
AAPL,150.00,2.50,1.69%,Buy,Strong performance
MSFT,300.00,-1.50,-0.50%,Hold,Stable growth
GOOGL,2500.00,15.00,0.60%,Buy,Innovation leader";
        }

        public static StockQuoteResponse CreateSuccessfulStockResponse(string symbol = "AAPL")
        {
            return new StockQuoteResponse
            {
                Success = true,
                Data = CreateTestStockData(symbol)
            };
        }

        public static StockQuoteResponse CreateFailedStockResponse(string errorMessage = "Stock not found")
        {
            return new StockQuoteResponse
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }

    public static class MockExtensions
    {
        public static string GetRandomSessionId()
        {
            return Guid.NewGuid().ToString();
        }

        public static void VerifyLoggerCalled<T>(this Mock<ILogger<T>> mockLogger, LogLevel level, string message)
        {
            mockLogger.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
