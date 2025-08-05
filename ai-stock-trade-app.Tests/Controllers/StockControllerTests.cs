using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ai_stock_trade_app.Controllers;
using ai_stock_trade_app.Models;
using ai_stock_trade_app.Services;
using Microsoft.AspNetCore.Http;

namespace ai_stock_trade_app.Tests.Controllers
{
    public class StockControllerTests
    {
        private readonly Mock<IStockDataService> _mockStockDataService;
        private readonly Mock<IAIAnalysisService> _mockAIAnalysisService;
        private readonly Mock<IWatchlistService> _mockWatchlistService;
        private readonly Mock<ILogger<StockController>> _mockLogger;
        private readonly StockController _controller;
        private readonly Mock<HttpContext> _mockHttpContext;
        private readonly Mock<ISession> _mockSession;

        public StockControllerTests()
        {
            _mockStockDataService = new Mock<IStockDataService>();
            _mockAIAnalysisService = new Mock<IAIAnalysisService>();
            _mockWatchlistService = new Mock<IWatchlistService>();
            _mockLogger = new Mock<ILogger<StockController>>();

            _controller = new StockController(
                _mockStockDataService.Object,
                _mockAIAnalysisService.Object,
                _mockWatchlistService.Object,
                _mockLogger.Object);

            // Setup HttpContext and Session mocking
            _mockHttpContext = new Mock<HttpContext>();
            _mockSession = new Mock<ISession>();
            _mockHttpContext.Setup(x => x.Session).Returns(_mockSession.Object);
            _controller.ControllerContext.HttpContext = _mockHttpContext.Object;

            // Setup session to return a test session ID
            var sessionId = "test-session-id";
            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);
        }

        [Fact]
        public async Task AddStock_ValidSymbol_ReturnsSuccessResult()
        {
            // Arrange
            var request = new AddStockRequest { Symbol = "AAPL" };
            var stockResponse = new StockQuoteResponse
            {
                Success = true,
                Data = new StockData
                {
                    Symbol = "AAPL",
                    Price = 150.00m,
                    Change = 2.50m,
                    PercentChange = "1.69%"
                }
            };

            _mockStockDataService.Setup(x => x.GetStockQuoteAsync("AAPL"))
                .ReturnsAsync(stockResponse);

            _mockWatchlistService.Setup(x => x.AddToWatchlistAsync(It.IsAny<string>(), "AAPL"))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.AddStock(request);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = jsonResult.Value;
            Assert.NotNull(data);
            
            var successProperty = data.GetType().GetProperty("success");
            var messageProperty = data.GetType().GetProperty("message");
            
            Assert.NotNull(successProperty);
            Assert.NotNull(messageProperty);
            Assert.True((bool)successProperty.GetValue(data)!);
            Assert.Equal("Added AAPL to watchlist", messageProperty.GetValue(data));
        }

        [Fact]
        public async Task AddStock_InvalidSymbol_ReturnsErrorResult()
        {
            // Arrange
            var request = new AddStockRequest { Symbol = "INVALID" };
            var stockResponse = new StockQuoteResponse
            {
                Success = false,
                ErrorMessage = "Stock not found"
            };

            _mockStockDataService.Setup(x => x.GetStockQuoteAsync("INVALID"))
                .ReturnsAsync(stockResponse);

            // Act
            var result = await _controller.AddStock(request);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = jsonResult.Value;
            Assert.NotNull(data);
            
            var successProperty = data.GetType().GetProperty("success");
            var messageProperty = data.GetType().GetProperty("message");
            
            Assert.NotNull(successProperty);
            Assert.NotNull(messageProperty);
            Assert.False((bool)successProperty.GetValue(data)!);
            Assert.Equal("Stock not found", messageProperty.GetValue(data));
        }

        [Fact]
        public async Task RemoveStock_ValidSymbol_ReturnsSuccessResult()
        {
            // Arrange
            var symbol = "AAPL";
            _mockWatchlistService.Setup(x => x.RemoveFromWatchlistAsync(It.IsAny<string>(), symbol))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.RemoveStock(symbol);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = jsonResult.Value;
            Assert.NotNull(data);
            
            var successProperty = data.GetType().GetProperty("success");
            var messageProperty = data.GetType().GetProperty("message");
            
            Assert.NotNull(successProperty);
            Assert.NotNull(messageProperty);
            Assert.True((bool)successProperty.GetValue(data)!);
            Assert.Equal("Removed AAPL from watchlist", messageProperty.GetValue(data));
        }

        [Fact]
        public async Task GetChartData_ValidSymbol_ReturnsChartData()
        {
            // Arrange
            var symbol = "AAPL";
            var chartData = new List<ChartDataPoint>
            {
                new ChartDataPoint { Date = DateTime.Today.AddDays(-1), Price = 148.50m, Volume = 1000000 },
                new ChartDataPoint { Date = DateTime.Today, Price = 150.00m, Volume = 1100000 }
            };

            _mockStockDataService.Setup(x => x.GetHistoricalDataAsync(symbol, 30))
                .ReturnsAsync(chartData);

            // Act
            var result = await _controller.GetChartData(symbol);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = jsonResult.Value;
            Assert.NotNull(data);
            
            var successProperty = data.GetType().GetProperty("success");
            var dataProperty = data.GetType().GetProperty("data");
            
            Assert.NotNull(successProperty);
            Assert.NotNull(dataProperty);
            Assert.True((bool)successProperty.GetValue(data)!);
            Assert.Equal(chartData, dataProperty.GetValue(data));
        }

        [Fact]
        public async Task ClearWatchlist_ReturnsSuccessResult()
        {
            // Arrange
            _mockWatchlistService.Setup(x => x.ClearWatchlistAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.ClearWatchlist();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = jsonResult.Value;
            Assert.NotNull(data);
            
            var successProperty = data.GetType().GetProperty("success");
            var messageProperty = data.GetType().GetProperty("message");
            
            Assert.NotNull(successProperty);
            Assert.NotNull(messageProperty);
            Assert.True((bool)successProperty.GetValue(data)!);
            Assert.Equal("Watchlist cleared", messageProperty.GetValue(data));
        }

        [Fact]
        public void ParseCsvContent_ValidCsv_ReturnsExportData()
        {
            // Arrange
            var csvContent = @"Ticker,Price,Change,Percent,Recommendation,Analysis
AAPL,150.00,2.50,1.69%,Buy,Strong performance
MSFT,300.00,-1.00,-0.33%,Hold,Stable stock";

            // Act
            var result = InvokeParseCSvContent(csvContent);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Watchlist.Count);
            
            var aaplItem = result.Watchlist.First(w => w.Symbol == "AAPL");
            Assert.NotNull(aaplItem.StockData);
            Assert.Equal(150.00m, aaplItem.StockData.Price);
            Assert.Equal(2.50m, aaplItem.StockData.Change);
            Assert.Equal("Buy", aaplItem.StockData.Recommendation);
            
            var msftItem = result.Watchlist.First(w => w.Symbol == "MSFT");
            Assert.NotNull(msftItem.StockData);
            Assert.Equal(300.00m, msftItem.StockData.Price);
            Assert.Equal(-1.00m, msftItem.StockData.Change);
            Assert.Equal("Hold", msftItem.StockData.Recommendation);
        }

        [Fact]
        public void ParseCsvContent_InvalidLines_SkipsInvalidData()
        {
            // Arrange
            var csvContent = @"Ticker,Price,Change,Percent,Recommendation,Analysis
AAPL,150.00,2.50,1.69%,Buy,Strong performance
INVALID,DATA
MSFT,300.00,-1.00,-0.33%,Hold,Stable stock";

            // Act
            var result = InvokeParseCSvContent(csvContent);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Watchlist.Count); // Should skip the invalid line
            Assert.Contains(result.Watchlist, w => w.Symbol == "AAPL");
            Assert.Contains(result.Watchlist, w => w.Symbol == "MSFT");
        }

        // Helper method to invoke the private ParseCsvContent method
        private ExportData InvokeParseCSvContent(string csvContent)
        {
            var method = typeof(StockController).GetMethod("ParseCsvContent", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);
            
            var result = method.Invoke(null, new object[] { csvContent });
            Assert.NotNull(result);
            
            return (ExportData)result;
        }
    }
}
