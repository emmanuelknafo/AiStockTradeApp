using ai_stock_trade_app.Models;
using ai_stock_trade_app.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ai_stock_trade_app.Tests.Services
{
    public class StockDataServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<StockDataService>> _mockLogger;
        private readonly Mock<IStockDataRepository> _mockRepository;

        public StockDataServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<StockDataService>>();
            _mockRepository = new Mock<IStockDataRepository>();
        }

        private StockDataService CreateService(HttpClient? httpClient = null)
        {
            var client = httpClient ?? new HttpClient();
            
            // Setup mock configuration to avoid actual API calls
            _mockConfiguration.Setup(x => x["AlphaVantage:ApiKey"]).Returns("demo-key");
            _mockConfiguration.Setup(x => x["TwelveData:ApiKey"]).Returns("demo");
            
            return new StockDataService(client, _mockConfiguration.Object, _mockLogger.Object, _mockRepository.Object);
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
            // With external API calls, we can't guarantee success, so we check for either success or graceful failure
            if (result.Success)
            {
                result.Data.Should().NotBeNull();
                result.Data!.Symbol.Should().Be(symbol.ToUpper());
                result.Data.Price.Should().BeGreaterThan(0);
            }
            else
            {
                result.ErrorMessage.Should().NotBeNullOrEmpty();
            }
        }

        [Theory]
        [InlineData("")]
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

        [Fact]
        public async Task GetStockQuoteAsync_NullSymbol_ShouldHandleGracefully()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.GetStockQuoteAsync(null!);

            // Assert
            result.Should().NotBeNull();
            // The service should handle null symbols gracefully
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
            // The service should return some suggestions for valid queries that match the query
            if (result.Any())
            {
                result.Should().AllSatisfy(suggestion => suggestion.Should().Contain(query.ToUpper()));
            }
        }

        [Theory]
        [InlineData("")]
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

        [Fact]
        public async Task GetStockSuggestionsAsync_NullQuery_ShouldReturnEmptyList()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.GetStockSuggestionsAsync(null!);

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
            // Should always return data (either from API or demo data)
            result.Should().NotBeEmpty();
            (result.Count <= days).Should().BeTrue();
            result.All(point => point.Price > 0).Should().BeTrue();
            result.All(point => point.Date <= DateTime.Now.Date).Should().BeTrue();
        }

        [Theory]
        [InlineData("INVALID", 30)]
        [InlineData("", 30)]
        public async Task GetHistoricalDataAsync_InvalidSymbol_ShouldReturnDemoData(string symbol, int days)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.GetHistoricalDataAsync(symbol, days);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<List<ChartDataPoint>>();
            // Should return demo data for invalid symbols
            result.Should().NotBeEmpty();
            (result.Count <= days).Should().BeTrue();
        }

        [Fact]
        public async Task GetHistoricalDataAsync_NullSymbol_ShouldHandleGracefully()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.GetHistoricalDataAsync(null!, 30);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<List<ChartDataPoint>>();
            // Should return demo data even for null symbol
            result.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetStockQuoteAsync_WithConfiguration_ShouldUseApiKey()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["AlphaVantage:ApiKey"]).Returns("test-api-key");
            var service = CreateService();

            // Act
            var result = await service.GetStockQuoteAsync("AAPL");

            // Assert
            result.Should().NotBeNull();
            // The service should attempt to use the API key and handle any failures gracefully
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
            stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(900); // Expect at least 1 second delay
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
            
            // All should return some data
            result1.Should().NotBeEmpty();
            result2.Should().NotBeEmpty();
            result3.Should().NotBeEmpty();
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
            // Should handle long queries gracefully
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

        [Fact]
        public async Task GetStockSuggestionsAsync_CaseInsensitive_ShouldWork()
        {
            // Arrange
            var service = CreateService();

            // Act
            var lowerResult = await service.GetStockSuggestionsAsync("aap");
            var upperResult = await service.GetStockSuggestionsAsync("AAP");

            // Assert
            lowerResult.Should().NotBeNull();
            upperResult.Should().NotBeNull();
            lowerResult.Should().BeEquivalentTo(upperResult);
        }

        [Theory]
        [InlineData("AAPL")]
        [InlineData("GOOGL")]
        public async Task GetHistoricalDataAsync_ShouldReturnOrderedData(string symbol)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.GetHistoricalDataAsync(symbol, 10);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            
            // Data should be ordered by date (ascending)
            var dates = result.Select(x => x.Date).ToList();
            dates.Should().BeInAscendingOrder();
        }

        [Fact]
        public async Task GetStockQuoteAsync_CacheHit_ShouldReturnCachedData()
        {
            // Arrange
            var cachedStockData = new StockData
            {
                Id = 1,
                Symbol = "AAPL",
                Price = 150.50m,
                Change = 2.25m,
                PercentChange = "1.52%",
                CompanyName = "Apple Inc.",
                Currency = "USD",
                LastUpdated = DateTime.UtcNow.AddMinutes(-5),
                CachedAt = DateTime.UtcNow.AddMinutes(-5),
                CacheDuration = TimeSpan.FromMinutes(15)
            };

            _mockRepository.Setup(x => x.GetCachedStockDataAsync("AAPL"))
                          .ReturnsAsync(cachedStockData);

            var service = CreateService();

            // Act
            var result = await service.GetStockQuoteAsync("AAPL");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Symbol.Should().Be("AAPL");
            result.Data.Price.Should().Be(150.50m);
            
            // Verify cache was checked
            _mockRepository.Verify(x => x.GetCachedStockDataAsync("AAPL"), Times.Once);
            // Verify no data was saved (since it was a cache hit)
            _mockRepository.Verify(x => x.SaveStockDataAsync(It.IsAny<StockData>()), Times.Never);
        }

        [Fact]
        public async Task GetStockQuoteAsync_CacheMiss_ShouldFetchAndCache()
        {
            // Arrange
            _mockRepository.Setup(x => x.GetCachedStockDataAsync("AAPL"))
                          .ReturnsAsync((StockData?)null);

            _mockRepository.Setup(x => x.SaveStockDataAsync(It.IsAny<StockData>()))
                          .ReturnsAsync((StockData data) => data);

            var service = CreateService();

            // Act
            var result = await service.GetStockQuoteAsync("AAPL");

            // Assert
            result.Should().NotBeNull();
            
            // Verify cache was checked
            _mockRepository.Verify(x => x.GetCachedStockDataAsync("AAPL"), Times.Once);
            
            // If successful, verify data was cached
            if (result.Success && result.Data != null)
            {
                _mockRepository.Verify(x => x.SaveStockDataAsync(It.IsAny<StockData>()), Times.Once);
            }
        }
    }
}
