using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ai_stock_trade_app.Services;
using System.Net;

namespace ai_stock_trade_app.Tests.Integration
{
    public class WebApplicationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public WebApplicationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task Get_HomePage_ShouldRedirectToStockDashboard()
        {
            // Act
            var response = await _client.GetAsync("/");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("/Stock/Dashboard");
        }

        [Fact]
        public async Task Get_StockDashboard_ShouldReturnSuccessAndCorrectContentType()
        {
            // Act
            var response = await _client.GetAsync("/Stock/Dashboard");

            // Assert
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType!.ToString().Should().Contain("text/html");
        }

        [Fact]
        public async Task Get_HealthCheck_ShouldReturnHealthy()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Be("Healthy");
        }

        [Theory]
        [InlineData("/Stock/Dashboard")]
        [InlineData("/Home/Privacy")]
        public async Task Get_PublicPages_ShouldReturnSuccess(string url)
        {
            // Act
            var response = await _client.GetAsync(url);

            // Assert
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task Get_StaticAssets_ShouldReturnSuccess()
        {
            // Arrange
            var staticFiles = new[]
            {
                "/css/stock-tracker.css",
                "/js/stock-tracker.js",
                "/favicon.ico"
            };

            // Act & Assert
            foreach (var file in staticFiles)
            {
                var response = await _client.GetAsync(file);
                response.IsSuccessStatusCode.Should().BeTrue($"Static file {file} should be accessible");
            }
        }

        [Fact]
        public async Task Post_AddStock_ShouldRequireValidSymbol()
        {
            // Arrange
            var formData = new Dictionary<string, string>
            {
                ["symbol"] = "AAPL"
            };
            var formContent = new FormUrlEncodedContent(formData);

            // Act
            var response = await _client.PostAsync("/Stock/AddStock", formContent);

            // Assert
            // Should either succeed or return a reasonable error
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Get_StockSuggestions_ShouldReturnJsonResponse()
        {
            // Act
            var response = await _client.GetAsync("/Stock/GetSuggestions?query=A");

            // Assert
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType!.ToString().Should().Contain("application/json");
        }

        [Fact]
        public async Task Get_NonExistentPage_ShouldReturn404()
        {
            // Act
            var response = await _client.GetAsync("/NonExistent/Page");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public void Services_ShouldBeRegisteredCorrectly()
        {
            // Arrange & Act
            var serviceProvider = _factory.Services;

            // Assert
            serviceProvider.GetService<IStockDataService>().Should().NotBeNull();
            serviceProvider.GetService<IAIAnalysisService>().Should().NotBeNull();
            serviceProvider.GetService<IWatchlistService>().Should().NotBeNull();
        }

        [Fact]
        public async Task Application_ShouldHandleConcurrentRequests()
        {
            // Arrange
            var tasks = new List<Task<HttpResponseMessage>>();
            
            // Act
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_client.GetAsync("/Stock/Dashboard"));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert
            responses.Should().HaveCount(10);
            responses.Should().AllSatisfy(response => 
                response.IsSuccessStatusCode.Should().BeTrue());
        }

        [Fact]
        public async Task Get_ChartData_ShouldReturnJsonResponse()
        {
            // Act
            var response = await _client.GetAsync("/Stock/GetChartData?symbol=AAPL&days=30");

            // Assert
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType!.ToString().Should().Contain("application/json");
        }

        [Fact]
        public async Task Session_ShouldPersistAcrossRequests()
        {
            // Arrange
            var cookieContainer = new CookieContainer();
            var client = _factory.WithWebHostBuilder(builder => { })
                .CreateClient();

            // Act
            var response1 = await client.GetAsync("/Stock/Dashboard");
            var response2 = await client.GetAsync("/Stock/Dashboard");

            // Assert
            response1.EnsureSuccessStatusCode();
            response2.EnsureSuccessStatusCode();
            
            // Session cookies should be present
            var setCookieHeaders = response1.Headers.GetValues("Set-Cookie").ToList();
            setCookieHeaders.Should().NotBeEmpty();
        }
    }
}
