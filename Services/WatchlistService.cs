using ai_stock_trade_app.Models;
using System.Text.Json;

namespace ai_stock_trade_app.Services
{
    public interface IWatchlistService
    {
        Task<List<WatchlistItem>> GetWatchlistAsync(string sessionId);
        Task AddToWatchlistAsync(string sessionId, string symbol);
        Task RemoveFromWatchlistAsync(string sessionId, string symbol);
        Task ClearWatchlistAsync(string sessionId);
        Task<PortfolioSummary> CalculatePortfolioSummaryAsync(List<WatchlistItem> watchlist);
        Task<List<PriceAlert>> GetAlertsAsync(string sessionId);
        Task AddAlertAsync(string sessionId, PriceAlert alert);
        Task RemoveAlertAsync(string sessionId, string symbol, decimal targetPrice);
        Task<ExportData> GetExportDataAsync(string sessionId);
        Task ImportDataAsync(string sessionId, ExportData data);
    }

    public class WatchlistService : IWatchlistService
    {
        private readonly ILogger<WatchlistService> _logger;
        private static readonly Dictionary<string, List<WatchlistItem>> _watchlists = new();
        private static readonly Dictionary<string, List<PriceAlert>> _alerts = new();
        private readonly object _lock = new object();

        public WatchlistService(ILogger<WatchlistService> logger)
        {
            _logger = logger;
        }

        public Task<List<WatchlistItem>> GetWatchlistAsync(string sessionId)
        {
            lock (_lock)
            {
                if (!_watchlists.ContainsKey(sessionId))
                {
                    _watchlists[sessionId] = new List<WatchlistItem>();
                }
                return Task.FromResult(_watchlists[sessionId]);
            }
        }

        public Task AddToWatchlistAsync(string sessionId, string symbol)
        {
            lock (_lock)
            {
                if (!_watchlists.ContainsKey(sessionId))
                {
                    _watchlists[sessionId] = new List<WatchlistItem>();
                }

                var watchlist = _watchlists[sessionId];
                
                // Check if already exists
                if (!watchlist.Any(w => w.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
                {
                    // Check max size (same as JS app)
                    if (watchlist.Count >= 20)
                    {
                        throw new InvalidOperationException("Maximum watchlist size reached!");
                    }

                    watchlist.Add(new WatchlistItem
                    {
                        Symbol = symbol.ToUpper(),
                        AddedDate = DateTime.UtcNow
                    });
                }
            }
            return Task.CompletedTask;
        }

        public Task RemoveFromWatchlistAsync(string sessionId, string symbol)
        {
            lock (_lock)
            {
                if (_watchlists.ContainsKey(sessionId))
                {
                    _watchlists[sessionId].RemoveAll(w => w.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
                }
            }
            return Task.CompletedTask;
        }

        public Task ClearWatchlistAsync(string sessionId)
        {
            lock (_lock)
            {
                if (_watchlists.ContainsKey(sessionId))
                {
                    _watchlists[sessionId].Clear();
                }
            }
            return Task.CompletedTask;
        }

        public Task<PortfolioSummary> CalculatePortfolioSummaryAsync(List<WatchlistItem> watchlist)
        {
            var summary = new PortfolioSummary
            {
                LastUpdated = DateTime.UtcNow
            };

            var validStocks = watchlist.Where(w => w.StockData != null).ToList();
            
            if (validStocks.Any())
            {
                summary.TotalValue = validStocks.Sum(w => w.StockData!.Price);
                summary.TotalChange = validStocks.Sum(w => w.StockData!.Change);
                summary.StockCount = validStocks.Count;
                
                // Calculate percentage change
                var totalPreviousValue = summary.TotalValue - summary.TotalChange;
                if (totalPreviousValue > 0)
                {
                    summary.TotalChangePercent = (summary.TotalChange / totalPreviousValue) * 100;
                }
            }

            return Task.FromResult(summary);
        }

        public Task<List<PriceAlert>> GetAlertsAsync(string sessionId)
        {
            lock (_lock)
            {
                if (!_alerts.ContainsKey(sessionId))
                {
                    _alerts[sessionId] = new List<PriceAlert>();
                }
                return Task.FromResult(_alerts[sessionId].Where(a => !a.IsTriggered).ToList());
            }
        }

        public Task AddAlertAsync(string sessionId, PriceAlert alert)
        {
            lock (_lock)
            {
                if (!_alerts.ContainsKey(sessionId))
                {
                    _alerts[sessionId] = new List<PriceAlert>();
                }
                _alerts[sessionId].Add(alert);
            }
            return Task.CompletedTask;
        }

        public Task RemoveAlertAsync(string sessionId, string symbol, decimal targetPrice)
        {
            lock (_lock)
            {
                if (_alerts.ContainsKey(sessionId))
                {
                    _alerts[sessionId].RemoveAll(a => 
                        a.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) && 
                        a.TargetPrice == targetPrice);
                }
            }
            return Task.CompletedTask;
        }

        public async Task<ExportData> GetExportDataAsync(string sessionId)
        {
            var watchlist = await GetWatchlistAsync(sessionId);
            var portfolio = await CalculatePortfolioSummaryAsync(watchlist);

            return new ExportData
            {
                Watchlist = watchlist,
                Portfolio = portfolio,
                ExportDate = DateTime.UtcNow
            };
        }

        public async Task ImportDataAsync(string sessionId, ExportData data)
        {
            if (data.Watchlist?.Any() == true)
            {
                lock (_lock)
                {
                    if (!_watchlists.ContainsKey(sessionId))
                    {
                        _watchlists[sessionId] = new List<WatchlistItem>();
                    }

                    var currentWatchlist = _watchlists[sessionId];
                    
                    // Merge and deduplicate
                    foreach (var item in data.Watchlist)
                    {
                        if (!currentWatchlist.Any(w => w.Symbol.Equals(item.Symbol, StringComparison.OrdinalIgnoreCase)))
                        {
                            currentWatchlist.Add(item);
                        }
                    }
                }
            }
        }
    }
}
