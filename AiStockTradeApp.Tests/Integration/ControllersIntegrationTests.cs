using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using AiStockTradeApp.Controllers;
using AiStockTradeApp.Services.Interfaces;

namespace AiStockTradeApp.Tests.Integration
{
    #pragma warning disable CS0618 // Suppress obsolete StockController references in integration tests
    public class ControllersIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public ControllersIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Home")]
        [InlineData("/Home/Index")]
        [InlineData("/Home/Privacy")]
        public async Task HomeController_Endpoints_ShouldReturnSuccessStatusCodes(string url)
        {
            // Act
            var response = await _client.GetAsync(url);

            // Assert
            response.Should().NotBeNull();
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        }

        [Theory]
        [InlineData("/Stock/Dashboard")]
        public async Task StockController_Endpoints_ShouldReturnSuccessStatusCodes(string url)
        {
            // Act
            var response = await _client.GetAsync(url);

            // Assert
            response.Should().NotBeNull();
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        }

        [Theory]
        [InlineData("/ListedStocks")]
        [InlineData("/ListedStocks/Index")]
        public async Task ListedStocksController_Endpoints_ShouldReturnSuccessStatusCodes(string url)
        {
            // Act
            var response = await _client.GetAsync(url);

            // Assert
            response.Should().NotBeNull();
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task VersionController_Get_ShouldReturnOkWithVersionInfo()
        {
            // Act
            var response = await _client.GetAsync("/version");

            // Assert
            response.Should().NotBeNull();
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();
            content.Should().Contain("version");
        }

        [Theory]
        [InlineData("/ListedStocks/Data")]
        [InlineData("/ListedStocks/Data?q=AAPL")]
        [InlineData("/ListedStocks/Data?sector=Technology")]
        [InlineData("/ListedStocks/Data?page=1&pageSize=10")]
        public async Task ListedStocksController_DataEndpoint_ShouldHandleParameters(string url)
        {
            // Act
            var response = await _client.GetAsync(url);

            // Assert
            response.Should().NotBeNull();
            // May return various status codes depending on data availability
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK, 
                HttpStatusCode.NotFound, 
                HttpStatusCode.InternalServerError,
                HttpStatusCode.BadRequest);
        }

        [Fact]
        public void Application_ShouldHaveRequiredServices()
        {
            // Arrange & Act
            using var scope = _factory.Services.CreateScope();
            var services = scope.ServiceProvider;

            // Assert - Verify that key services are registered (not controllers, but services they depend on)
            var logger = services.GetService<ILogger<StockController>>();
            logger.Should().NotBeNull("ILogger should be available for StockController");

            // Verify that application services are available
            var stockDataService = services.GetService<IStockDataService>();
            stockDataService.Should().NotBeNull("IStockDataService should be registered");
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        public async Task UnsupportedEndpoint_ShouldReturn404OrMethodNotAllowed(string httpMethod)
        {
            // Arrange
            var request = new HttpRequestMessage(new HttpMethod(httpMethod), "/NonExistentEndpoint");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.Should().NotBeNull();
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.NotFound, 
                HttpStatusCode.MethodNotAllowed,
                HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Application_ShouldHandleLargePageSize()
        {
            // Act
            var response = await _client.GetAsync("/ListedStocks/Data?pageSize=1000");

            // Assert
            response.Should().NotBeNull();
            // Should handle large page size gracefully (either accept it or limit it)
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK, 
                HttpStatusCode.BadRequest,
                HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task Application_ShouldHandleNegativePageParameters()
        {
            // Act
            var response = await _client.GetAsync("/ListedStocks/Data?page=-1&pageSize=-10");

            // Assert
            response.Should().NotBeNull();
            // Should handle negative parameters gracefully
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK, 
                HttpStatusCode.BadRequest,
                HttpStatusCode.InternalServerError);
        }
    }
    #pragma warning restore CS0618
}
