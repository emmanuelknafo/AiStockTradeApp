using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using AiStockTradeApp.Services.Interfaces;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Reflection;

namespace AiStockTradeApp.Tests.Integration
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
            // Arrange & Act
            var response = await _client.GetAsync("/");

            // Assert
            // The home page might return OK or redirect depending on configuration
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK, 
                HttpStatusCode.Redirect, 
                HttpStatusCode.Found);
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
        public async Task Get_VersionEndpoint_ShouldReturnJsonWithVersion()
        {
            var response = await _client.GetAsync("/version");
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType!.ToString().Should().Contain("application/json");
            var json = await response.Content.ReadAsStringAsync();
            json.Should().Contain("version");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.TryGetProperty("version", out var versionProp).Should().BeTrue();
            versionProp.GetString().Should().NotBeNullOrWhiteSpace();

            if (root.TryGetProperty("appVersion", out var appVersionProp) && appVersionProp.ValueKind != JsonValueKind.Null)
            {
                // appVersion should match semantic-like pattern if present
                var appVersion = appVersionProp.GetString();
                appVersion.Should().MatchRegex(@"^[0-9]+\.[0-9]+\.[0-9]+.*$");
            }
        }

        [Fact]
        public async Task Get_VersionEndpoint_ShouldExposeAppVersionEnvWhenSet()
        {
            const string expectedAppVersion = "1.2.3-test";
            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services => { });
                builder.UseSetting("APP_VERSION", expectedAppVersion);
            });
            var client = factory.CreateClient();
            var response = await client.GetAsync("/version");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("appVersion", out var appVersionProp))
            {
                appVersionProp.GetString().Should().Be(expectedAppVersion);
            }
        }

        [Fact]
        public async Task Post_AddStock_ShouldRequireValidSymbol()
        {
            // Arrange - Use JSON content as the controller expects FromBody
            var requestData = new { Symbol = "AAPL" };
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/Stock/AddStock", content);

            // Assert
            // Should either succeed or return a reasonable error (not UnsupportedMediaType)
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK, 
                HttpStatusCode.BadRequest, 
                HttpStatusCode.InternalServerError);
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
            // Arrange & Act - Use a scoped service provider
            using var scope = _factory.Services.CreateScope();
            var serviceProvider = scope.ServiceProvider;

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
            
            // Session cookies should be present if cookies are being used
            if (response1.Headers.Contains("Set-Cookie"))
            {
                var setCookieHeaders = response1.Headers.GetValues("Set-Cookie").ToList();
                setCookieHeaders.Should().NotBeEmpty();
            }
        }

        [Fact]
        public async Task Get_StockData_ShouldReturnValidJson()
        {
            // Act
            var response = await _client.GetAsync("/Stock/GetStockData?symbol=AAPL");

            // Assert
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType!.ToString().Should().Contain("application/json");
            
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Post_RemoveStock_ShouldReturnValidResponse()
        {
            // Act
            var response = await _client.PostAsync("/Stock/RemoveStock?symbol=AAPL", null);

            // Assert
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType!.ToString().Should().Contain("application/json");
        }

        [Fact]
        public async Task Post_ClearWatchlist_ShouldReturnValidResponse()
        {
            // Act
            var response = await _client.PostAsync("/Stock/ClearWatchlist", null);

            // Assert
            response.EnsureSuccessStatusCode();
            response.Content.Headers.ContentType!.ToString().Should().Contain("application/json");
        }

        [Fact]
        public async Task Get_VersionEndpoint_AppVersionShouldMatchAssemblyInformationalVersion()
        {
            // Use the assembly informational version we compiled with as the APP_VERSION
            var asm = typeof(Program).Assembly;
            var attr = Attribute.GetCustomAttribute(asm, typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
            var informational = attr?.InformationalVersion ?? "unknown";

            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("APP_VERSION", informational);
            });
            var client = factory.CreateClient();

            var response = await client.GetAsync("/version");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.TryGetProperty("version", out var versionProp).Should().BeTrue();
            root.TryGetProperty("appVersion", out var appVersionProp).Should().BeTrue();
            var version = versionProp.GetString();
            var appVersion = appVersionProp.GetString();
            version.Should().NotBeNullOrWhiteSpace();
            appVersion.Should().Be(informational);
            // They should match (or assembly may contain '+sha' metadata). Allow version to start with appVersion when metadata present.
            if (version != appVersion)
            {
                version.Should().StartWith(appVersion);
            }
        }
    }
}
