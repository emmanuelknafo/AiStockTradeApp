using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Implementations;
using AiStockTradeApp.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace AiStockTradeApp.Tests.Services;

public class WatchlistQuoteAggregatorTests
{
    private readonly Mock<IStockDataService> _stockData = new();
    private readonly Mock<ILogger<WatchlistQuoteAggregator>> _logger = new();

    [Fact]
    public async Task PopulateQuotesAsync_PartialFailure_ReturnsError()
    {
        var watchlist = new List<WatchlistItem>
        {
            new() { Symbol = "AAA" },
            new() { Symbol = "BBB" }
        };

        _stockData.Setup(s => s.GetStockQuoteAsync("AAA"))
            .ReturnsAsync(new StockQuoteResponse { Success = true, Data = new StockData { Symbol = "AAA", Price = 10m } });
        _stockData.Setup(s => s.GetStockQuoteAsync("BBB"))
            .ReturnsAsync(new StockQuoteResponse { Success = false, ErrorMessage = "Rate limit" });

        var agg = new WatchlistQuoteAggregator(_stockData.Object, _logger.Object);
        var errors = await agg.PopulateQuotesAsync(watchlist);

        errors.Should().Contain(e => e.Contains("Rate limit"));
        watchlist.First(w => w.Symbol == "AAA").StockData.Should().NotBeNull();
        watchlist.First(w => w.Symbol == "BBB").StockData.Should().BeNull();
    }
}
