using System.Net;
using System.Net.Http.Json;
using AiStockTradeApp.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
using ai_stock_trade_app.Api;

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
                        ["USE_INMEMORY_DB"] = "true"
                    }!);
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
            Assert.True(resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.NotFound);
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
            var list = await _client.GetFromJsonAsync<List<string>>("/api/stocks/suggestions?query=AA");
            Assert.NotNull(list);
        }

        [Theory]
        [InlineData("AAPL", 10)]
        public async Task Historical_Endpoint_Should_Return_List(string symbol, int days)
        {
            var list = await _client.GetFromJsonAsync<List<ChartDataPoint>>($"/api/stocks/historical?symbol={symbol}&days={days}");
            Assert.NotNull(list);
        }
    }
}
