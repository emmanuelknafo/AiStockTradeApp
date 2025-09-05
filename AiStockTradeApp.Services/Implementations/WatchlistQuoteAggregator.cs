using System.Collections.Concurrent;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AiStockTradeApp.Services.Implementations
{
    public class WatchlistQuoteAggregator : IWatchlistQuoteAggregator
    {
        private readonly IStockDataService _stockDataService;
        private readonly ILogger<WatchlistQuoteAggregator> _logger;

        public WatchlistQuoteAggregator(IStockDataService stockDataService, ILogger<WatchlistQuoteAggregator> logger)
        {
            _stockDataService = stockDataService;
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> PopulateQuotesAsync(List<WatchlistItem> watchlist, CancellationToken cancellationToken = default)
        {
            if (watchlist == null || watchlist.Count == 0)
            {
                return Array.Empty<string>();
            }

            var errors = new ConcurrentBag<string>();

            var tasks = watchlist.Select(async item =>
            {
                try
                {
                    var quote = await _stockDataService.GetStockQuoteAsync(item.Symbol);
                    if (quote.Success && quote.Data != null)
                    {
                        item.StockData = quote.Data;
                    }
                    else
                    {
                        var msg = !string.IsNullOrWhiteSpace(quote.ErrorMessage)
                            ? quote.ErrorMessage
                            : $"Failed to load {item.Symbol}";
                        errors.Add(msg);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{item.Symbol}: {ex.Message}");
                    _logger.LogWarning(ex, "Failed to load stock data for {Symbol}", item.Symbol);
                }
            });

            await Task.WhenAll(tasks);

            // Fallback: if nothing recorded but some items remain unpopulated
            if (!errors.Any() && watchlist.Any(w => w.StockData == null))
            {
                errors.Add("Some symbols failed to load");
            }

            return errors
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .Take(3)
                .ToList();
        }
    }
}
