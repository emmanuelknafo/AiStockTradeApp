using AiStockTradeApp.Services.Interfaces;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Entities.ViewModels;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.DataAccess;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AiStockTradeApp.Services.Implementations
{
    /// <summary>
    /// User-aware watchlist service that automatically persists data for authenticated users
    /// Falls back to session-based storage for anonymous users
    /// </summary>
    public class UserWatchlistService : IUserWatchlistService
    {
        private readonly ILogger<UserWatchlistService> _logger;
        private readonly StockDataContext _context;
        
        // Fallback session storage for anonymous users
        private static readonly Dictionary<string, List<WatchlistItem>> _sessionWatchlists = new();
        private static readonly Dictionary<string, List<PriceAlert>> _sessionAlerts = new();
        private readonly object _lock = new object();

        public UserWatchlistService(ILogger<UserWatchlistService> logger, StockDataContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<List<WatchlistItem>> GetWatchlistAsync(string? userId = null, string? sessionId = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    // Return user's persistent watchlist
                    var userItems = await _context.UserWatchlistItems
                        .Where(w => w.UserId == userId)
                        .OrderBy(w => w.SortOrder)
                        .ThenBy(w => w.AddedAt)
                        .ToListAsync();

                    return userItems.Select(item => new WatchlistItem
                    {
                        Symbol = item.Symbol,
                        AddedDate = item.AddedAt
                    }).ToList();
                }
                else if (!string.IsNullOrEmpty(sessionId))
                {
                    // Return session-based watchlist for anonymous users
                    lock (_lock)
                    {
                        if (!_sessionWatchlists.ContainsKey(sessionId))
                        {
                            _sessionWatchlists[sessionId] = new List<WatchlistItem>();
                        }
                        return _sessionWatchlists[sessionId].ToList();
                    }
                }

                return new List<WatchlistItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting watchlist for userId: {UserId}, sessionId: {SessionId}", userId, sessionId);
                return new List<WatchlistItem>();
            }
        }

        public async Task AddToWatchlistAsync(string symbol, string? userId = null, string? sessionId = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    // Add to user's persistent watchlist
                    var existingItem = await _context.UserWatchlistItems
                        .FirstOrDefaultAsync(w => w.UserId == userId && w.Symbol == symbol.ToUpper());

                    if (existingItem == null)
                    {
                        // Check watchlist size limit
                        var currentCount = await _context.UserWatchlistItems
                            .CountAsync(w => w.UserId == userId);

                        if (currentCount >= 20)
                        {
                            throw new InvalidOperationException("Maximum watchlist size reached!");
                        }

                        var newItem = new UserWatchlistItem
                        {
                            UserId = userId,
                            Symbol = symbol.ToUpper(),
                            AddedAt = DateTime.UtcNow,
                            SortOrder = currentCount
                        };

                        _context.UserWatchlistItems.Add(newItem);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Added {Symbol} to user {UserId} watchlist", symbol, userId);
                    }
                }
                else if (!string.IsNullOrEmpty(sessionId))
                {
                    // Add to session-based watchlist for anonymous users
                    lock (_lock)
                    {
                        if (!_sessionWatchlists.ContainsKey(sessionId))
                        {
                            _sessionWatchlists[sessionId] = new List<WatchlistItem>();
                        }

                        var watchlist = _sessionWatchlists[sessionId];
                        
                        if (!watchlist.Any(w => w.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (watchlist.Count >= 20)
                            {
                                throw new InvalidOperationException("Maximum watchlist size reached!");
                            }

                            watchlist.Add(new WatchlistItem
                            {
                                Symbol = symbol.ToUpper(),
                                AddedDate = DateTime.UtcNow
                            });

                            _logger.LogInformation("Added {Symbol} to session {SessionId} watchlist", symbol, sessionId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding {Symbol} to watchlist for userId: {UserId}, sessionId: {SessionId}", 
                    symbol, userId, sessionId);
                throw;
            }
        }

        public async Task RemoveFromWatchlistAsync(string symbol, string? userId = null, string? sessionId = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    // Remove from user's persistent watchlist
                    var item = await _context.UserWatchlistItems
                        .FirstOrDefaultAsync(w => w.UserId == userId && w.Symbol == symbol.ToUpper());

                    if (item != null)
                    {
                        _context.UserWatchlistItems.Remove(item);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Removed {Symbol} from user {UserId} watchlist", symbol, userId);
                    }
                }
                else if (!string.IsNullOrEmpty(sessionId))
                {
                    // Remove from session-based watchlist
                    lock (_lock)
                    {
                        if (_sessionWatchlists.ContainsKey(sessionId))
                        {
                            _sessionWatchlists[sessionId].RemoveAll(w => 
                                w.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

                            _logger.LogInformation("Removed {Symbol} from session {SessionId} watchlist", symbol, sessionId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing {Symbol} from watchlist for userId: {UserId}, sessionId: {SessionId}", 
                    symbol, userId, sessionId);
                throw;
            }
        }

        public async Task ClearWatchlistAsync(string? userId = null, string? sessionId = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    // Clear user's persistent watchlist
                    var userItems = await _context.UserWatchlistItems
                        .Where(w => w.UserId == userId)
                        .ToListAsync();

                    _context.UserWatchlistItems.RemoveRange(userItems);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Cleared watchlist for user {UserId}", userId);
                }
                else if (!string.IsNullOrEmpty(sessionId))
                {
                    // Clear session-based watchlist
                    lock (_lock)
                    {
                        if (_sessionWatchlists.ContainsKey(sessionId))
                        {
                            _sessionWatchlists[sessionId].Clear();
                            _logger.LogInformation("Cleared watchlist for session {SessionId}", sessionId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing watchlist for userId: {UserId}, sessionId: {SessionId}", userId, sessionId);
                throw;
            }
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

        public async Task<List<PriceAlert>> GetAlertsAsync(string? userId = null, string? sessionId = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    // Return user's persistent alerts
                    var userAlerts = await _context.UserPriceAlerts
                        .Where(a => a.UserId == userId && a.IsActive)
                        .OrderByDescending(a => a.CreatedAt)
                        .ToListAsync();

                    return userAlerts.Select(alert => new PriceAlert
                    {
                        Symbol = alert.Symbol,
                        TargetPrice = alert.TargetValue,
                        AlertType = alert.AlertType,
                        CreatedDate = alert.CreatedAt,
                        IsTriggered = false // Active alerts are not triggered
                    }).ToList();
                }
                else if (!string.IsNullOrEmpty(sessionId))
                {
                    // Return session-based alerts
                    lock (_lock)
                    {
                        if (!_sessionAlerts.ContainsKey(sessionId))
                        {
                            _sessionAlerts[sessionId] = new List<PriceAlert>();
                        }
                        return _sessionAlerts[sessionId].Where(a => !a.IsTriggered).ToList();
                    }
                }

                return new List<PriceAlert>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alerts for userId: {UserId}, sessionId: {SessionId}", userId, sessionId);
                return new List<PriceAlert>();
            }
        }

        public async Task AddAlertAsync(PriceAlert alert, string? userId = null, string? sessionId = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    // Add to user's persistent alerts
                    var userAlert = new UserPriceAlert
                    {
                        UserId = userId,
                        Symbol = alert.Symbol.ToUpper(),
                        AlertType = alert.AlertType,
                        TargetValue = alert.TargetPrice,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.UserPriceAlerts.Add(userAlert);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Added alert for {Symbol} at {TargetPrice} for user {UserId}", 
                        alert.Symbol, alert.TargetPrice, userId);
                }
                else if (!string.IsNullOrEmpty(sessionId))
                {
                    // Add to session-based alerts
                    lock (_lock)
                    {
                        if (!_sessionAlerts.ContainsKey(sessionId))
                        {
                            _sessionAlerts[sessionId] = new List<PriceAlert>();
                        }
                        _sessionAlerts[sessionId].Add(alert);

                        _logger.LogInformation("Added alert for {Symbol} at {TargetPrice} for session {SessionId}", 
                            alert.Symbol, alert.TargetPrice, sessionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding alert for {Symbol} for userId: {UserId}, sessionId: {SessionId}", 
                    alert.Symbol, userId, sessionId);
                throw;
            }
        }

        public async Task RemoveAlertAsync(string symbol, decimal targetPrice, string? userId = null, string? sessionId = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    // Remove from user's persistent alerts
                    var userAlert = await _context.UserPriceAlerts
                        .FirstOrDefaultAsync(a => a.UserId == userId && 
                                                  a.Symbol == symbol.ToUpper() && 
                                                  a.TargetValue == targetPrice && 
                                                  a.IsActive);

                    if (userAlert != null)
                    {
                        userAlert.IsActive = false;
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Removed alert for {Symbol} at {TargetPrice} for user {UserId}", 
                            symbol, targetPrice, userId);
                    }
                }
                else if (!string.IsNullOrEmpty(sessionId))
                {
                    // Remove from session-based alerts
                    lock (_lock)
                    {
                        if (_sessionAlerts.ContainsKey(sessionId))
                        {
                            _sessionAlerts[sessionId].RemoveAll(a => 
                                a.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) && 
                                a.TargetPrice == targetPrice);

                            _logger.LogInformation("Removed alert for {Symbol} at {TargetPrice} for session {SessionId}", 
                                symbol, targetPrice, sessionId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing alert for {Symbol} for userId: {UserId}, sessionId: {SessionId}", 
                    symbol, userId, sessionId);
                throw;
            }
        }

        public async Task<ExportData> GetExportDataAsync(string? userId = null, string? sessionId = null)
        {
            try
            {
                var watchlist = await GetWatchlistAsync(userId, sessionId);
                var portfolio = await CalculatePortfolioSummaryAsync(watchlist);

                return new ExportData
                {
                    Watchlist = watchlist,
                    Portfolio = portfolio,
                    ExportDate = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting export data for userId: {UserId}, sessionId: {SessionId}", userId, sessionId);
                throw;
            }
        }

        public async Task ImportDataAsync(ExportData data, string? userId = null, string? sessionId = null)
        {
            try
            {
                if (data.Watchlist?.Any() == true)
                {
                    foreach (var item in data.Watchlist)
                    {
                        await AddToWatchlistAsync(item.Symbol, userId, sessionId);
                    }

                    _logger.LogInformation("Imported {Count} watchlist items for userId: {UserId}, sessionId: {SessionId}", 
                        data.Watchlist.Count, userId, sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing data for userId: {UserId}, sessionId: {SessionId}", userId, sessionId);
                throw;
            }
        }

        public async Task<List<UserWatchlistItem>> GetUserWatchlistItemsAsync(string userId)
        {
            try
            {
                return await _context.UserWatchlistItems
                    .Where(w => w.UserId == userId)
                    .OrderBy(w => w.SortOrder)
                    .ThenBy(w => w.AddedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user watchlist items for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<UserPriceAlert>> GetUserAlertsAsync(string userId)
        {
            try
            {
                return await _context.UserPriceAlerts
                    .Where(a => a.UserId == userId && a.IsActive)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user alerts for user {UserId}", userId);
                throw;
            }
        }

        public async Task UpdateWatchlistItemAsync(UserWatchlistItem item)
        {
            try
            {
                _context.UserWatchlistItems.Update(item);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Updated watchlist item {Id} for user {UserId}", item.Id, item.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating watchlist item {Id}", item.Id);
                throw;
            }
        }

        public async Task UpdateAlertAsync(UserPriceAlert alert)
        {
            try
            {
                _context.UserPriceAlerts.Update(alert);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Updated alert {Id} for user {UserId}", alert.Id, alert.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating alert {Id}", alert.Id);
                throw;
            }
        }

        public async Task MigrateSessionToUserAsync(string sessionId, string userId)
        {
            try
            {
                // Get session watchlist
                var sessionWatchlist = await GetWatchlistAsync(null, sessionId);
                var sessionAlerts = await GetAlertsAsync(null, sessionId);

                // Migrate watchlist items
                foreach (var item in sessionWatchlist)
                {
                    await AddToWatchlistAsync(item.Symbol, userId);
                }

                // Migrate alerts
                foreach (var alert in sessionAlerts)
                {
                    await AddAlertAsync(alert, userId);
                }

                // Clear session data
                await ClearWatchlistAsync(null, sessionId);
                lock (_lock)
                {
                    if (_sessionAlerts.ContainsKey(sessionId))
                    {
                        _sessionAlerts[sessionId].Clear();
                    }
                }

                _logger.LogInformation("Migrated session {SessionId} data to user {UserId}: {WatchlistCount} watchlist items, {AlertCount} alerts", 
                    sessionId, userId, sessionWatchlist.Count, sessionAlerts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating session {SessionId} to user {UserId}", sessionId, userId);
                throw;
            }
        }

        public async Task ReorderWatchlistAsync(string userId, List<int> itemIds)
        {
            try
            {
                var items = await _context.UserWatchlistItems
                    .Where(w => w.UserId == userId && itemIds.Contains(w.Id))
                    .ToListAsync();

                for (int i = 0; i < itemIds.Count; i++)
                {
                    var item = items.FirstOrDefault(w => w.Id == itemIds[i]);
                    if (item != null)
                    {
                        item.SortOrder = i;
                    }
                }

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Reordered watchlist for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering watchlist for user {UserId}", userId);
                throw;
            }
        }

        public async Task SetWatchlistItemAliasAsync(int itemId, string? alias)
        {
            try
            {
                var item = await _context.UserWatchlistItems.FindAsync(itemId);
                if (item != null)
                {
                    item.Alias = alias;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Set alias for watchlist item {Id} to '{Alias}'", itemId, alias);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting alias for watchlist item {Id}", itemId);
                throw;
            }
        }

        public async Task SetWatchlistItemTargetPriceAsync(int itemId, decimal? targetPrice)
        {
            try
            {
                var item = await _context.UserWatchlistItems.FindAsync(itemId);
                if (item != null)
                {
                    item.TargetPrice = targetPrice;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Set target price for watchlist item {Id} to {TargetPrice}", itemId, targetPrice);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting target price for watchlist item {Id}", itemId);
                throw;
            }
        }

        public async Task UpdateWatchlistItemAliasAsync(int itemId, string userId, string? alias)
        {
            try
            {
                var item = await _context.UserWatchlistItems
                    .FirstOrDefaultAsync(w => w.Id == itemId && w.UserId == userId);

                if (item != null)
                {
                    item.Alias = alias;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated alias for watchlist item {Id} for user {UserId}", itemId, userId);
                }
                else
                {
                    _logger.LogWarning("Watchlist item {Id} not found for user {UserId}", itemId, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating alias for watchlist item {Id} for user {UserId}", itemId, userId);
                throw;
            }
        }

        public async Task UpdateWatchlistItemTargetPriceAsync(int itemId, string userId, decimal? targetPrice)
        {
            try
            {
                var item = await _context.UserWatchlistItems
                    .FirstOrDefaultAsync(w => w.Id == itemId && w.UserId == userId);

                if (item != null)
                {
                    item.TargetPrice = targetPrice;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated target price for watchlist item {Id} for user {UserId}", itemId, userId);
                }
                else
                {
                    _logger.LogWarning("Watchlist item {Id} not found for user {UserId}", itemId, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating target price for watchlist item {Id} for user {UserId}", itemId, userId);
                throw;
            }
        }

        public async Task UpdateWatchlistItemAlertsAsync(int itemId, string userId, bool enabled)
        {
            try
            {
                var item = await _context.UserWatchlistItems
                    .FirstOrDefaultAsync(w => w.Id == itemId && w.UserId == userId);

                if (item != null)
                {
                    item.EnableAlerts = enabled;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated alerts setting for watchlist item {Id} for user {UserId}", itemId, userId);
                }
                else
                {
                    _logger.LogWarning("Watchlist item {Id} not found for user {UserId}", itemId, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating alerts for watchlist item {Id} for user {UserId}", itemId, userId);
                throw;
            }
        }

        public async Task RemoveFromWatchlistByIdAsync(int itemId, string userId)
        {
            try
            {
                var item = await _context.UserWatchlistItems
                    .FirstOrDefaultAsync(w => w.Id == itemId && w.UserId == userId);

                if (item != null)
                {
                    _context.UserWatchlistItems.Remove(item);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Removed watchlist item {Id} for user {UserId}", itemId, userId);
                }
                else
                {
                    _logger.LogWarning("Watchlist item {Id} not found for user {UserId}", itemId, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing watchlist item {Id} for user {UserId}", itemId, userId);
                throw;
            }
        }
    }
}
