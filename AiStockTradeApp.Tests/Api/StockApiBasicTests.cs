using System.Net;
using System.Net.Http.Json;
using AiStockTradeApp.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
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
                    // Clear existing configuration and add test-specific config
                    cfg.Sources.Clear();
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["USE_INMEMORY_DB"] = "true",
                        ["AlphaVantage:ApiKey"] = "",
                        ["TwelveData:ApiKey"] = "",
                        ["ApplicationInsights:ConnectionString"] = "",
                        ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                        ["Logging:LogLevel:Default"] = "Error",
                        ["Logging:LogLevel:Microsoft"] = "Error",
                        ["Logging:LogLevel:System"] = "Error"
                    }!);
                });

                builder.ConfigureServices(services =>
                {
                    // Remove ALL potentially problematic services
                    var servicesToRemove = services.Where(s => 
                        s.ServiceType.FullName?.Contains("ApplicationInsights") == true ||
                        s.ServiceType.FullName?.Contains("Swagger") == true ||
                        s.ServiceType.FullName?.Contains("TelemetryClient") == true ||
                        s.ServiceType.FullName?.Contains("TelemetryConfiguration") == true ||
                        s.ServiceType.FullName?.Contains("ApiDescriptions") == true ||
                        s.ServiceType.FullName?.Contains("DocumentProvider") == true ||
                        s.ServiceType.FullName?.Contains("EndpointsApi") == true)
                        .ToList();

                    foreach (var service in servicesToRemove)
                    {
                        services.Remove(service);
                    }

                    // Configure minimal logging for tests
                    services.AddLogging(builder =>
                    {
                        builder.ClearProviders();
                        builder.AddConsole();
                        builder.SetMinimumLevel(LogLevel.Error);
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
                $"Expected OK or NotFound but got {resp.StatusCode}. Response: {await resp.Content.ReadAsStringAsync()}");
            
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
            
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                var errorContent = await resp.Content.ReadAsStringAsync();
                Assert.True(false, $"Expected OK but got {resp.StatusCode}. Response: {errorContent}");
            }
            
            var list = await resp.Content.ReadFromJsonAsync<List<string>>();
            Assert.NotNull(list);
        }

        [Theory]
        [InlineData("AAPL", 10)]
        public async Task Historical_Endpoint_Should_Return_List(string symbol, int days)
        {
            var resp = await _client.GetAsync($"/api/stocks/historical?symbol={symbol}&days={days}");
            
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                var errorContent = await resp.Content.ReadAsStringAsync();
                Assert.True(false, $"Expected OK but got {resp.StatusCode}. Response: {errorContent}");
            }
            
            var list = await resp.Content.ReadFromJsonAsync<List<ChartDataPoint>>();
            Assert.NotNull(list);
            Assert.True(list.Count > 0, "Historical data should contain at least one data point");
        }

        [Fact]
        public async Task Health_Endpoint_Should_Return_Ok()
        {
            var resp = await _client.GetAsync("/health");
            
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                var errorContent = await resp.Content.ReadAsStringAsync();
                Assert.True(false, $"Expected OK but got {resp.StatusCode}. Response: {errorContent}");
            }
            
            var content = await resp.Content.ReadAsStringAsync();
            Assert.Equal("OK", content);
        }
    }
}
