using AiStockTradeApp.Entities;
using AiStockTradeApp.Entities.ViewModels;

namespace AiStockTradeApp.Services.Interfaces
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
}