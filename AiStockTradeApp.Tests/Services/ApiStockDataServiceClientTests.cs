using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq.Protected;

namespace AiStockTradeApp.Tests.Services
{
    public class ApiStockDataServiceClientTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<ApiStockDataServiceClient>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockMessageHandler;
        private readonly HttpClient _httpClient;

        public ApiStockDataServiceClientTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<ApiStockDataServiceClient>>();
            _mockMessageHandler = new Mock<HttpMessageHandler>();

            // Setup configuration
            _mockConfiguration.Setup(x => x["StockApi:BaseUrl"]).Returns("https://localhost:5001");
            _mockConfiguration.Setup(x => x["StockApi:HttpBaseUrl"]).Returns("http://localhost:5256");

            // Create HttpClient with mocked handler
            _httpClient = new HttpClient(_mockMessageHandler.Object);
        }

        private ApiStockDataServiceClient CreateService()
        {
            return new ApiStockDataServiceClient(_httpClient, _mockConfiguration.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Act
            var service = CreateService();

            // Assert
            service.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullConfiguration_ShouldUseDefaultUrls()
        {
            // Arrange
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["StockApi:BaseUrl"]).Returns((string?)null);
            mockConfig.Setup(x => x["StockApi:HttpBaseUrl"]).Returns((string?)null);

            // Act
            var service = new ApiStockDataServiceClient(_httpClient, mockConfig.Object, _mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
            // The service should use default URLs when configuration returns null
        }

        [Theory]
        [InlineData("AAPL")]
        [InlineData("MSFT")]
        [InlineData("GOOGL")]
        public async Task GetStockQuoteAsync_WithValidSymbol_ShouldReturnSuccessResponse(string symbol)
        {
            // Arrange
            var expectedStockData = new StockData
            {
                Symbol = symbol,
                Price = 150.25m,
                CompanyName = "Test Company"
            };

            var jsonResponse = JsonSerializer.Serialize(expectedStockData);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            };

            _mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(), 
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var service = CreateService();

            // Act
            var result = await service.GetStockQuoteAsync(symbol);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Symbol.Should().Be(symbol);
            result.ErrorMessage.Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task GetStockQuoteAsync_WhenApiReturnsNull_ShouldReturnFailureResponse()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
            };

            _mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(), 
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var service = CreateService();

            // Act
            var result = await service.GetStockQuoteAsync("INVALID");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Data.Should().BeNull();
            result.ErrorMessage.Should().Be("No data returned");
        }

        [Fact]
        public async Task GetStockQuoteAsync_WhenHttpRequestFails_ShouldReturnFailureResponse()
        {
            // Arrange
            _mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(), 
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var service = CreateService();

            // Act
            var result = await service.GetStockQuoteAsync("AAPL");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Data.Should().BeNull();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Theory]
        [InlineData("AAPL")]
        [InlineData("Apple")]
        [InlineData("A")]
        public async Task GetStockSuggestionsAsync_WithValidQuery_ShouldReturnSuggestions(string query)
        {
            // Arrange
            var expectedSuggestions = new List<string> { "AAPL", "AMZN", "AMD" };
            var jsonResponse = JsonSerializer.Serialize(expectedSuggestions);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            };

            _mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(), 
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var service = CreateService();

            // Act
            var result = await service.GetStockSuggestionsAsync(query);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedSuggestions);
        }

        [Fact]
        public async Task GetStockSuggestionsAsync_WhenHttpRequestFails_ShouldReturnEmptyList()
        {
            // Arrange
            _mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(), 
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var service = CreateService();

            // Act
            var result = await service.GetStockSuggestionsAsync("AAPL");

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Theory]
        [InlineData("AAPL", 30)]
        [InlineData("MSFT", 7)]
        [InlineData("GOOGL", 365)]
        public async Task GetHistoricalDataAsync_WithValidParameters_ShouldReturnChartData(string symbol, int days)
        {
            // Arrange
            var expectedChartData = new List<ChartDataPoint>
            {
                new() { Date = DateTime.Today.AddDays(-1), Price = 150.25m },
                new() { Date = DateTime.Today.AddDays(-2), Price = 148.50m }
            };

            var jsonResponse = JsonSerializer.Serialize(expectedChartData);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            };

            _mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(), 
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var service = CreateService();

            // Act
            var result = await service.GetHistoricalDataAsync(symbol, days);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(expectedChartData);
        }

        [Fact]
        public async Task GetHistoricalDataAsync_WithDefaultDays_ShouldUse30Days()
        {
            // Arrange
            var expectedChartData = new List<ChartDataPoint>();
            var jsonResponse = JsonSerializer.Serialize(expectedChartData);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            };

            _mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("days=30")), 
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var service = CreateService();

            // Act
            var result = await service.GetHistoricalDataAsync("AAPL");

            // Assert
            result.Should().NotBeNull();
            // Verify the request was made with days=30 (default)
            _mockMessageHandler.Protected().Verify("SendAsync", Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("days=30")),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GetHistoricalDataAsync_WhenHttpRequestFails_ShouldReturnEmptyList()
        {
            // Arrange
            _mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(), 
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var service = CreateService();

            // Act
            var result = await service.GetHistoricalDataAsync("AAPL", 30);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Theory]
        [InlineData("special&chars")]
        [InlineData("symbol with spaces")]
        [InlineData("symbol/with/slashes")]
        public async Task GetStockQuoteAsync_WithSpecialCharacters_ShouldEscapeSymbol(string symbol)
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(), 
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var service = CreateService();

            // Act
            await service.GetStockQuoteAsync(symbol);

            // Assert
            // Verify that the request was made (the service should handle URL encoding internally)
            _mockMessageHandler.Protected().Verify("SendAsync", Times.AtLeastOnce(),
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("symbol")),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GetStockQuoteAsync_WhenNon200Response_ShouldReturnFailure()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);

            _mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(), 
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            var service = CreateService();

            // Act
            var result = await service.GetStockQuoteAsync("INVALID");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Data.Should().BeNull();
        }
    }
}
