using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using ai_stock_trade_app.Controllers;
using ai_stock_trade_app.Models;
using ai_stock_trade_app.Services;
using Microsoft.Extensions.Logging;

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

            // Setup HttpContext and Session
            _mockSession = new Mock<ISession>();
            _mockHttpContext = new Mock<HttpContext>();
            _mockHttpContext.Setup(x => x.Session).Returns(_mockSession.Object);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = _mockHttpContext.Object
            };
        }

        [Fact]
        public async Task Dashboard_EmptyWatchlist_ShouldReturnViewWithEmptyModel()
        {
            // Arrange
            var sessionId = "test-session";
            var emptyWatchlist = new List<WatchlistItem>();
            
            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);
            _mockWatchlistService.Setup(x => x.GetWatchlistAsync(sessionId))
                .ReturnsAsync(emptyWatchlist);

            // Act
            var result = await _controller.Dashboard();

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = result as ViewResult;
            viewResult!.Model.Should().BeOfType<DashboardViewModel>();
            
            var model = viewResult.Model as DashboardViewModel;
            model!.Watchlist.Should().BeEmpty();
        }

        [Fact]
        public async Task Dashboard_WithWatchlistItems_ShouldReturnViewWithPopulatedModel()
        {
            // Arrange
            var sessionId = "test-session";
            var stockData = new StockData { Symbol = "AAPL", CompanyName = "Apple Inc.", Price = 150m, Change = 2.5m, PercentChange = "+1.69%" };
            var watchlist = new List<WatchlistItem>
            {
                new WatchlistItem { Symbol = "AAPL", StockData = stockData }
            };

            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);
            _mockWatchlistService.Setup(x => x.GetWatchlistAsync(sessionId))
                .ReturnsAsync(watchlist);

            var stockResponse = new StockQuoteResponse
            {
                Success = true,
                Data = new StockData { Symbol = "AAPL", Price = 155m, Change = 5m, PercentChange = "+3.33%" }
            };

            _mockStockDataService.Setup(x => x.GetStockQuoteAsync("AAPL"))
                .ReturnsAsync(stockResponse);

            _mockAIAnalysisService.Setup(x => x.GenerateAnalysisAsync("AAPL", It.IsAny<StockData>()))
                .ReturnsAsync(("Strong buy signal", "BUY", "Strong growth potential"));

            var portfolio = new PortfolioSummary { TotalValue = 155m, TotalChange = 5m };
            _mockWatchlistService.Setup(x => x.CalculatePortfolioSummaryAsync(It.IsAny<List<WatchlistItem>>()))
                .ReturnsAsync(portfolio);

            _mockWatchlistService.Setup(x => x.GetAlertsAsync(sessionId))
                .ReturnsAsync(new List<PriceAlert>());

            // Act
            var result = await _controller.Dashboard();

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = result as ViewResult;
            var model = viewResult!.Model as DashboardViewModel;
            model!.Watchlist.Should().HaveCount(1);
        }

        [Fact]
        public async Task AddStock_ValidSymbol_ShouldReturnSuccessJson()
        {
            // Arrange
            var sessionId = "test-session";
            var symbol = "AAPL";
            var request = new AddStockRequest { Symbol = symbol };
            
            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);

            var stockResponse = new StockQuoteResponse
            {
                Success = true,
                Data = new StockData 
                { 
                    Symbol = symbol, 
                    CompanyName = "Apple Inc.", 
                    Price = 150m,
                    Change = 2.5m,
                    PercentChange = "+1.69%"
                }
            };

            _mockStockDataService.Setup(x => x.GetStockQuoteAsync(symbol))
                .ReturnsAsync(stockResponse);

            // Act
            var result = await _controller.AddStock(request);

            // Assert
            result.Should().BeOfType<JsonResult>();
            var jsonResult = result as JsonResult;
            var responseData = jsonResult!.Value;
            
            responseData.Should().NotBeNull();
            // Verify that AddToWatchlistAsync was called
            _mockWatchlistService.Verify(x => x.AddToWatchlistAsync(sessionId, symbol), Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AddStock_InvalidSymbol_ShouldReturnErrorJson(string symbol)
        {
            // Arrange
            var sessionId = "test-session";
            var request = new AddStockRequest { Symbol = symbol };
            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);

            // Act
            var result = await _controller.AddStock(request);

            // Assert
            result.Should().BeOfType<JsonResult>();
            var jsonResult = result as JsonResult;
            jsonResult!.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task AddStock_NullSymbol_ShouldReturnErrorJson()
        {
            // Arrange
            var sessionId = "test-session";
            var request = new AddStockRequest { Symbol = null! };
            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);

            // Act
            var result = await _controller.AddStock(request);

            // Assert
            result.Should().BeOfType<JsonResult>();
            var jsonResult = result as JsonResult;
            jsonResult!.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task RemoveStock_ExistingSymbol_ShouldReturnSuccessJson()
        {
            // Arrange
            var sessionId = "test-session";
            var symbol = "AAPL";
            
            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);

            // Act
            var result = await _controller.RemoveStock(symbol);

            // Assert
            result.Should().BeOfType<JsonResult>();
            _mockWatchlistService.Verify(x => x.RemoveFromWatchlistAsync(sessionId, symbol), Times.Once);
        }

        [Fact]
        public async Task ClearWatchlist_ShouldReturnSuccessJson()
        {
            // Arrange
            var sessionId = "test-session";
            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);

            // Act
            var result = await _controller.ClearWatchlist();

            // Assert
            result.Should().BeOfType<JsonResult>();
            _mockWatchlistService.Verify(x => x.ClearWatchlistAsync(sessionId), Times.Once);
        }

        [Fact]
        public async Task GetSuggestions_ValidQuery_ShouldReturnSuggestions()
        {
            // Arrange
            var query = "APP";
            var sessionId = "test-session";
            var suggestions = new List<string> { "AAPL", "APPL", "APPS" };
            var watchlist = new List<WatchlistItem>();
            
            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);
            _mockWatchlistService.Setup(x => x.GetWatchlistAsync(sessionId))
                .ReturnsAsync(watchlist);
            _mockStockDataService.Setup(x => x.GetStockSuggestionsAsync(query))
                .ReturnsAsync(suggestions);

            // Act
            var result = await _controller.GetSuggestions(query);

            // Assert
            result.Should().BeOfType<JsonResult>();
            var jsonResult = result as JsonResult;
            jsonResult!.Value.Should().BeEquivalentTo(suggestions);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetSuggestions_EmptyQuery_ShouldReturnEmptyList(string query)
        {
            // Act
            var result = await _controller.GetSuggestions(query);

            // Assert
            result.Should().BeOfType<JsonResult>();
            var jsonResult = result as JsonResult;
            jsonResult!.Value.Should().BeEquivalentTo(new List<string>());
        }

        [Fact]
        public async Task GetChartData_ValidSymbol_ShouldReturnChartData()
        {
            // Arrange
            var symbol = "AAPL";
            var days = 30;
            var chartData = new List<ChartDataPoint>
            {
                new ChartDataPoint { Date = DateTime.Today.AddDays(-1), Price = 150m, Volume = 1000000 },
                new ChartDataPoint { Date = DateTime.Today, Price = 155m, Volume = 1200000 }
            };

            _mockStockDataService.Setup(x => x.GetHistoricalDataAsync(symbol, days))
                .ReturnsAsync(chartData);

            // Act
            var result = await _controller.GetChartData(symbol, days);

            // Assert
            result.Should().BeOfType<JsonResult>();
            var jsonResult = result as JsonResult;
            var response = jsonResult!.Value;
            response.Should().NotBeNull();
        }

        [Fact]
        public async Task GetStockData_ValidSymbol_ShouldReturnStockDataWithAIAnalysis()
        {
            // Arrange
            var sessionId = "test-session";
            var symbol = "AAPL";
            
            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);

            var stockResponse = new StockQuoteResponse
            {
                Success = true,
                Data = new StockData { Symbol = symbol, Price = 160m, Change = 10m, PercentChange = "+6.67%" }
            };

            _mockStockDataService.Setup(x => x.GetStockQuoteAsync(symbol))
                .ReturnsAsync(stockResponse);

            _mockAIAnalysisService.Setup(x => x.GenerateAnalysisAsync(symbol, It.IsAny<StockData>()))
                .ReturnsAsync(("Updated analysis", "BUY", "Strong fundamentals"));

            // Act
            var result = await _controller.GetStockData(symbol);

            // Assert
            result.Should().BeOfType<JsonResult>();
            var jsonResult = result as JsonResult;
            var response = jsonResult!.Value;
            response.Should().NotBeNull();
        }

        [Fact]
        public void GetSessionId_NewSession_ShouldCreateNewSessionId()
        {
            // Arrange
            _mockSession.Setup(x => x.GetString("SessionId")).Returns((string?)null);
            _mockSession.Setup(x => x.SetString("SessionId", It.IsAny<string>()));

            // Act
            var result = _controller.Dashboard();

            // Assert
            _mockSession.Verify(x => x.SetString("SessionId", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ExportCsv_WithData_ShouldReturnFileResult()
        {
            // Arrange
            var sessionId = "test-session";
            var watchlist = new List<WatchlistItem>
            {
                new WatchlistItem 
                { 
                    Symbol = "AAPL", 
                    StockData = new StockData { Symbol = "AAPL", CompanyName = "Apple Inc.", Price = 150m }
                }
            };
            var exportData = new ExportData
            {
                Watchlist = watchlist,
                Portfolio = new PortfolioSummary()
            };

            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);
            _mockWatchlistService.Setup(x => x.GetExportDataAsync(sessionId))
                .ReturnsAsync(exportData);

            // Act
            var result = await _controller.ExportCsv();

            // Assert
            result.Should().BeOfType<FileContentResult>();
            var fileResult = result as FileContentResult;
            fileResult!.ContentType.Should().Be("text/csv");
            fileResult.FileDownloadName.Should().Contain("watchlist");
        }

        [Fact]
        public async Task ExportJson_WithData_ShouldReturnFileResult()
        {
            // Arrange
            var sessionId = "test-session";
            var watchlist = new List<WatchlistItem>
            {
                new WatchlistItem 
                { 
                    Symbol = "AAPL", 
                    StockData = new StockData { Symbol = "AAPL", CompanyName = "Apple Inc.", Price = 150m }
                }
            };
            var exportData = new ExportData
            {
                Watchlist = watchlist,
                Portfolio = new PortfolioSummary()
            };

            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);
            _mockWatchlistService.Setup(x => x.GetExportDataAsync(sessionId))
                .ReturnsAsync(exportData);

            // Act
            var result = await _controller.ExportJson();

            // Assert
            result.Should().BeOfType<FileContentResult>();
            var fileResult = result as FileContentResult;
            fileResult!.ContentType.Should().Be("application/json");
            fileResult.FileDownloadName.Should().Contain("watchlist");
        }

        [Fact]
        public async Task RefreshAll_ShouldReturnUpdatedData()
        {
            // Arrange
            var sessionId = "test-session";
            var stockData = new StockData { Symbol = "AAPL", Price = 150m, Change = 2.5m, PercentChange = "+1.69%" };
            var watchlist = new List<WatchlistItem>
            {
                new WatchlistItem { Symbol = "AAPL", StockData = stockData }
            };

            _mockSession.Setup(x => x.GetString("SessionId")).Returns(sessionId);
            _mockWatchlistService.Setup(x => x.GetWatchlistAsync(sessionId))
                .ReturnsAsync(watchlist);

            var stockResponse = new StockQuoteResponse
            {
                Success = true,
                Data = new StockData { Symbol = "AAPL", Price = 155m, Change = 5m, PercentChange = "+3.33%" }
            };

            _mockStockDataService.Setup(x => x.GetStockQuoteAsync("AAPL"))
                .ReturnsAsync(stockResponse);

            _mockAIAnalysisService.Setup(x => x.GenerateAnalysisAsync("AAPL", It.IsAny<StockData>()))
                .ReturnsAsync(("Updated analysis", "BUY", "Strong fundamentals"));

            var portfolio = new PortfolioSummary { TotalValue = 155m, TotalChange = 5m };
            _mockWatchlistService.Setup(x => x.CalculatePortfolioSummaryAsync(It.IsAny<List<WatchlistItem>>()))
                .ReturnsAsync(portfolio);

            // Act
            var result = await _controller.RefreshAll();

            // Assert
            result.Should().BeOfType<JsonResult>();
            var jsonResult = result as JsonResult;
            var response = jsonResult!.Value;
            response.Should().NotBeNull();
        }
    }
}
