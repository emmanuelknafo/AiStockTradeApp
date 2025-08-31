using AiStockTradeApp.Entities;
using AiStockTradeApp.Entities.ViewModels;
using AiStockTradeApp.Entities.Models;

namespace AiStockTradeApp.Services.Interfaces
{
    /// <summary>
    /// Enhanced watchlist service that supports both authenticated users and session-based storage
    /// Automatically persists watchlists for logged-in users while maintaining session fallback
    /// </summary>
    public interface IUserWatchlistService
    {
        // Core watchlist operations that work for both authenticated and anonymous users
        Task<List<WatchlistItem>> GetWatchlistAsync(string? userId = null, string? sessionId = null);
        Task AddToWatchlistAsync(string symbol, string? userId = null, string? sessionId = null);
        Task RemoveFromWatchlistAsync(string symbol, string? userId = null, string? sessionId = null);
        Task ClearWatchlistAsync(string? userId = null, string? sessionId = null);
        
        // Portfolio calculations
        Task<PortfolioSummary> CalculatePortfolioSummaryAsync(List<WatchlistItem> watchlist);
        
        // Alert management
        Task<List<PriceAlert>> GetAlertsAsync(string? userId = null, string? sessionId = null);
        Task AddAlertAsync(PriceAlert alert, string? userId = null, string? sessionId = null);
        Task RemoveAlertAsync(string symbol, decimal targetPrice, string? userId = null, string? sessionId = null);
        
        // Import/Export functionality
        Task<ExportData> GetExportDataAsync(string? userId = null, string? sessionId = null);
        Task ImportDataAsync(ExportData data, string? userId = null, string? sessionId = null);
        
        // User-specific operations
        Task<List<UserWatchlistItem>> GetUserWatchlistItemsAsync(string userId);
        Task<List<UserPriceAlert>> GetUserAlertsAsync(string userId);
        Task UpdateWatchlistItemAsync(UserWatchlistItem item);
        Task UpdateAlertAsync(UserPriceAlert alert);
        
        // Migration support - move session data to user account
        Task MigrateSessionToUserAsync(string sessionId, string userId);
        
        // Watchlist management
        Task ReorderWatchlistAsync(string userId, List<int> itemIds);
        Task SetWatchlistItemAliasAsync(int itemId, string? alias);
        Task SetWatchlistItemTargetPriceAsync(int itemId, decimal? targetPrice);
        
        // Additional methods for the ManageWatchlist view
        Task UpdateWatchlistItemAliasAsync(int itemId, string userId, string? alias);
        Task UpdateWatchlistItemTargetPriceAsync(int itemId, string userId, decimal? targetPrice);
        Task UpdateWatchlistItemAlertsAsync(int itemId, string userId, bool enabled);
        Task RemoveFromWatchlistByIdAsync(int itemId, string userId);
    }
}
