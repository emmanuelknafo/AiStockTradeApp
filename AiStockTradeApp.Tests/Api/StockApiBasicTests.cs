using System.Net;
using System.Net.Http.Json;
using AiStockTradeApp.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using AiStockTradeApp.DataAccess;
using Xunit;
using AiStockTradeApp.Api;

namespace AiStockTradeApp.Tests.Api
{
    public class StockApiBasicTests : IClassFixture<WebApplicationFactory<ApiAssemblyMarker>>
    {
        private readonly HttpClient _client;

        public StockApiBasicTests(WebApplicationFactory<ApiAssemblyMarker> factory)
        {
            var configured = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["USE_INMEMORY_DB"] = "true",
                        ["AlphaVantage:ApiKey"] = "",
                        ["TwelveData:ApiKey"] = "",
                        ["ApplicationInsights:ConnectionString"] = "",
                        ["ASPNETCORE_ENVIRONMENT"] = "Development"
                    }!);
                });

                builder.ConfigureServices(services =>
                {
                    // Configure logging for tests to reduce noise
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.SetMinimumLevel(LogLevel.Warning);
                    });
                });
            });

            _client = configured.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://localhost")
            });
        }

        [Theory]
        [InlineData("AAPL")]
        [InlineData("MSFT")]
        public async Task Quote_Endpoint_Should_Return_Ok_Or_NotFound(string symbol)
        {
            var resp = await _client.GetAsync($"/api/stocks/quote?symbol={symbol}");
            
            // The endpoint should return OK with data or NotFound, but not 500 errors
            Assert.True(resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.NotFound, 
                $"Expected OK or NotFound but got {resp.StatusCode}");
            
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var data = await resp.Content.ReadFromJsonAsync<StockData>();
                Assert.NotNull(data);
                Assert.Equal(symbol, data!.Symbol);
            }
        }

        [Fact]
        public async Task Suggestions_Endpoint_Should_Return_List()
        {
            var resp = await _client.GetAsync("/api/stocks/suggestions?query=AA");
            resp.EnsureSuccessStatusCode();
            
            var list = await resp.Content.ReadFromJsonAsync<List<string>>();
            Assert.NotNull(list);
        }

        [Theory]
        [InlineData("AAPL", 10)]
        public async Task Historical_Endpoint_Should_Return_List(string symbol, int days)
        {
            var resp = await _client.GetAsync($"/api/stocks/historical?symbol={symbol}&days={days}");
            resp.EnsureSuccessStatusCode();
            
            var list = await resp.Content.ReadFromJsonAsync<List<ChartDataPoint>>();
            Assert.NotNull(list);
            Assert.True(list.Count > 0, "Historical data should contain at least one data point");
        }

        [Fact]
        public async Task Health_Endpoint_Should_Return_Ok()
        {
            var resp = await _client.GetAsync("/health");
            resp.EnsureSuccessStatusCode();
            
            var content = await resp.Content.ReadAsStringAsync();
            Assert.Equal("OK", content);
        }
    }
}
