using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ai_stock_trade_app.Services;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ai_stock_trade_app.Tests.Integration
{
    public class StockControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public StockControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Dashboard_Get_ReturnsSuccessAndCorrectContentType()
        {
            // Act
            var response = await _client.GetAsync("/Stock/Dashboard");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        }

        [Fact]
        public async Task GetSuggestions_ValidQuery_ReturnsJsonResponse()
        {
            // Act
            var response = await _client.GetAsync("/Stock/GetSuggestions?query=AAPL");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
            
            var content = await response.Content.ReadAsStringAsync();
            var suggestions = JsonSerializer.Deserialize<string[]>(content);
            Assert.NotNull(suggestions);
        }

        [Fact]
        public async Task GetChartData_ValidSymbol_ReturnsJsonResponse()
        {
            // Act
            var response = await _client.GetAsync("/Stock/GetChartData?symbol=AAPL&days=10");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
            
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("success", content);
            Assert.Contains("data", content);
        }

        [Fact]
        public async Task AddStock_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new { symbol = "AAPL" };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/Stock/AddStock", content);

            // Assert
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("success", responseContent);
        }

        [Fact]
        public async Task ExportCsv_ReturnsFileContent()
        {
            // Act
            var response = await _client.GetAsync("/Stock/ExportCsv");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Ticker,Price,Change,Percent,Recommendation,Analysis", content);
        }

        [Fact]
        public async Task ExportJson_ReturnsJsonFile()
        {
            // Act
            var response = await _client.GetAsync("/Stock/ExportJson");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            
            var content = await response.Content.ReadAsStringAsync();
            var exportData = JsonSerializer.Deserialize<JsonElement>(content);
            Assert.True(exportData.TryGetProperty("watchlist", out _));
            Assert.True(exportData.TryGetProperty("portfolio", out _));
        }

        [Fact]
        public async Task Home_Index_ReturnsSuccessAndCorrectContentType()
        {
            // Act
            var response = await _client.GetAsync("/");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        }

        [Fact]
        public async Task InvalidEndpoint_ReturnsNotFound()
        {
            // Act
            var response = await _client.GetAsync("/NonExistentEndpoint");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
