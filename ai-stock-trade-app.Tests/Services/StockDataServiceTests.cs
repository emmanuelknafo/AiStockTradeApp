using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ai_stock_trade_app.Models;
using ai_stock_trade_app.Services;
using System.Net;
using System.Text.Json;

namespace ai_stock_trade_app.Tests.Services
{
    public class StockDataServiceTests
    {
        private readonly Mock<HttpMessageHandler> _mockHttpHandler;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<StockDataService>> _mockLogger;
        private readonly HttpClient _httpClient;
        private readonly StockDataService _service;

        public StockDataServiceTests()
        {
            _mockHttpHandler = new Mock<HttpMessageHandler>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<StockDataService>>();
            
            _httpClient = new HttpClient(_mockHttpHandler.Object);
            _service = new StockDataService(_httpClient, _mockConfiguration.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetStockQuoteAsync_ValidSymbol_ReturnsStockData()
        {
            // Arrange
            var symbol = "AAPL";
            var mockResponse = new
            {
                chart = new
                {
                    result = new[]
                    {
                        new
                        {
                            meta = new
                            {
                                regularMarketPrice = 150.50,
                                previousClose = 148.00
                            }
                        }
                    }
                }
            };

            var jsonResponse = JsonSerializer.Serialize(mockResponse);
            
            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse)
                });

            // Act
            var result = await _service.GetStockQuoteAsync(symbol);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal("AAPL", result.Data.Symbol);
            Assert.Equal(150.50m, result.Data.Price);
            Assert.Equal(2.50m, result.Data.Change);
        }

        [Fact]
        public async Task GetStockQuoteAsync_HttpException_ReturnsFailure()
        {
            // Arrange
            var symbol = "INVALID";
            
            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _service.GetStockQuoteAsync(symbol);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Network error", result.ErrorMessage);
        }

        [Fact]
        public void GenerateDemoChartData_ValidParameters_ReturnsChartData()
        {
            // Arrange
            var symbol = "AAPL";
            var days = 5;

            // Act
            var result = InvokeGenerateDemoChartData(symbol, days);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(days, result.Count);
            Assert.All(result, point => Assert.True(point.Price > 0));
            Assert.All(result, point => Assert.True(point.Volume > 0));
            
            // Verify dates are in ascending order
            for (int i = 1; i < result.Count; i++)
            {
                Assert.True(result[i].Date > result[i - 1].Date);
            }
        }

        [Fact]
        public async Task GetHistoricalDataAsync_NoApiKey_ReturnsDemoData()
        {
            // Arrange
            var symbol = "AAPL";
            _mockConfiguration.Setup(x => x["AlphaVantage:ApiKey"]).Returns((string?)null);

            // Act
            var result = await _service.GetHistoricalDataAsync(symbol, 10);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10, result.Count);
            Assert.All(result, point => Assert.True(point.Price > 0));
        }

        [Theory]
        [InlineData("AAPL")]
        [InlineData("MSFT")]
        [InlineData("GOOGL")]
        public async Task GetStockSuggestionsAsync_ValidQuery_ReturnsFilteredSuggestions(string query)
        {
            // Act
            var result = await _service.GetStockSuggestionsAsync(query);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Count > 0);
            Assert.Contains(query.ToUpper(), result);
        }

        [Fact]
        public async Task GetStockSuggestionsAsync_EmptyQuery_ReturnsEmptyList()
        {
            // Act
            var result = await _service.GetStockSuggestionsAsync(string.Empty);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // Helper method to invoke the private GenerateDemoChartData method
        private List<ChartDataPoint> InvokeGenerateDemoChartData(string symbol, int days)
        {
            var method = typeof(StockDataService).GetMethod("GenerateDemoChartData", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            
            var result = method.Invoke(_service, new object[] { symbol, days });
            Assert.NotNull(result);
            
            return (List<ChartDataPoint>)result;
        }
    }
}
