using AiStockTradeApp.Entities;

namespace AiStockTradeApp.Services.Interfaces
{
    /// <summary>
    /// Aggregates quote retrieval for a watchlist, populating StockData and returning any error messages.
    /// </summary>
    public interface IWatchlistQuoteAggregator
    {
        /// <summary>
        /// Populate StockData for each watchlist item concurrently.
        /// Returns a distinct list of error messages (max 3) representing partial failures.
        /// </summary>
        Task<IReadOnlyList<string>> PopulateQuotesAsync(List<WatchlistItem> watchlist, CancellationToken cancellationToken = default);
    }
}
