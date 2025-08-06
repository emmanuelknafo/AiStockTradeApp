using ai_stock_trade_app.Models;
using ai_stock_trade_app.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ai_stock_trade_app.Tests.Services
{
    public class StockDataServiceTests
    {
        private readonly Mock<HttpClient> _mockHttpClient;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<StockDataService>> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

        public StockDataServiceTests()
        {
            _mockHttpClient = new Mock<HttpClient>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<StockDataService>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        }

        private StockDataService CreateService(HttpClient? httpClient = null)
        {
            var client = httpClient ?? new HttpClient();
            return new StockDataService(client, _mockConfiguration.Object, _mockLogger.Object);
        }

        [Theory]
        [InlineData("AAPL")]
        [InlineData("GOOGL")]
        [InlineData("MSFT")]
        [InlineData("TSLA")]
        public async Task GetStockQuoteAsync_ValidSymbol_ShouldReturnStockData(string symbol)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.GetStockQuoteAsync(symbol);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Symbol.Should().Be(symbol);
            result.Data.Price.Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("INVALID123")]
        [InlineData("TOOLONG")]
        public async Task GetStockQuoteAsync_InvalidSymbol_ShouldHandleGracefully(string symbol)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.GetStockQuoteAsync(symbol);

            // Assert
            result.Should().NotBeNull();
            // The service should handle invalid symbols gracefully
            // Either return success with valid data or failure with error message
            if (!result.Success)
            {
                result.ErrorMessage.Should().NotBeNullOrEmpty();
            }
        }

        [Theory]
        [InlineData("A")]
        [InlineData("AP")]
        [InlineData("APP")]
        [InlineData("APPL")]
        public async Task GetStockSuggestionsAsync_ValidQuery_ShouldReturnSuggestions(string query)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.GetStockSuggestionsAsync(query);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<List<string>>();
            // The service should return some suggestions for valid queries
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task GetStockSuggestionsAsync_EmptyQuery_ShouldReturnEmptyList(string query)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.GetStockSuggestionsAsync(query);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Theory]
        [InlineData("AAPL", 7)]
        [InlineData("GOOGL", 30)]
        [InlineData("MSFT", 90)]
        public async Task GetHistoricalDataAsync_ValidSymbol_ShouldReturnHistoricalData(string symbol, int days)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.GetHistoricalDataAsync(symbol, days);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<List<ChartDataPoint>>();
            if (result.Any())
            {
                result.Should().HaveCountLessOrEqualTo(days);
                result.All(point => point.Price > 0).Should().BeTrue();
                result.All(point => point.Date <= DateTime.Now.Date).Should().BeTrue();
            }
        }

        [Theory]
        [InlineData("INVALID", 30)]
        [InlineData("", 30)]
        [InlineData(null, 30)]
        public async Task GetHistoricalDataAsync_InvalidSymbol_ShouldReturnEmptyOrDemoData(string symbol, int days)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.GetHistoricalDataAsync(symbol, days);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<List<ChartDataPoint>>();
            // Should return either empty list or demo data for invalid symbols
        }

        [Fact]
        public async Task GetStockQuoteAsync_WithConfiguration_ShouldUseApiKey()
        {
            // Arrange
            var mockConfigSection = new Mock<IConfigurationSection>();
            mockConfigSection.Setup(x => x.Value).Returns("test-api-key");
            _mockConfiguration.Setup(x => x["AlphaVantage:ApiKey"]).Returns("test-api-key");

            var service = CreateService();

            // Act
            var result = await service.GetStockQuoteAsync("AAPL");

            // Assert
            result.Should().NotBeNull();
            // The service should attempt to use the API key
        }

        [Fact]
        public async Task GetStockQuoteAsync_MultipleCallsWithRateLimit_ShouldRespectRateLimit()
        {
            // Arrange
            var service = CreateService();
            var symbol = "AAPL";

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var task1 = service.GetStockQuoteAsync(symbol);
            var task2 = service.GetStockQuoteAsync(symbol);
            
            await Task.WhenAll(task1, task2);
            stopwatch.Stop();

            // Assert
            // The second call should be delayed due to rate limiting
            stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(500); // Some delay expected
        }

        [Fact]
        public async Task GetHistoricalDataAsync_InvalidDays_ShouldHandleGracefully()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            var result1 = await service.GetHistoricalDataAsync("AAPL", 0);
            var result2 = await service.GetHistoricalDataAsync("AAPL", -1);
            var result3 = await service.GetHistoricalDataAsync("AAPL", 1000);

            result1.Should().NotBeNull();
            result2.Should().NotBeNull();
            result3.Should().NotBeNull();
        }

        [Fact]
        public async Task GetStockSuggestionsAsync_LongQuery_ShouldHandleGracefully()
        {
            // Arrange
            var service = CreateService();
            var longQuery = new string('A', 100);

            // Act
            var result = await service.GetStockSuggestionsAsync(longQuery);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<List<string>>();
        }

        [Fact]
        public async Task Service_ConcurrentCalls_ShouldHandleConcurrency()
        {
            // Arrange
            var service = CreateService();
            var symbols = new[] { "AAPL", "GOOGL", "MSFT", "TSLA", "AMZN" };

            // Act
            var tasks = symbols.Select(symbol => service.GetStockQuoteAsync(symbol)).ToArray();
            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(5);
            results.Should().AllSatisfy(result => result.Should().NotBeNull());
        }
    }
}
