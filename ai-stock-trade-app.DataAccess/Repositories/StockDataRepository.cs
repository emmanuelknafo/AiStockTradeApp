using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiStockTradeApp.DataAccess.Repositories
{
    public class StockDataRepository : IStockDataRepository
    {
        private readonly StockDataContext _context;
        private readonly ILogger<StockDataRepository> _logger;

        public StockDataRepository(StockDataContext context, ILogger<StockDataRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<StockData?> GetCachedStockDataAsync(string symbol)
        {
            try
            {
                var cachedData = await _context.StockData
                    .Where(sd => sd.Symbol.ToUpper() == symbol.ToUpper())
                    .OrderByDescending(sd => sd.CachedAt)
                    .FirstOrDefaultAsync();

                if (cachedData != null && cachedData.IsCacheValid)
                {
                    _logger.LogInformation("Cache hit for symbol {Symbol}. Data cached at {CachedAt}", 
                        symbol, cachedData.CachedAt);
                    return cachedData;
                }

                if (cachedData != null)
                {
                    _logger.LogInformation("Cache expired for symbol {Symbol}. Data cached at {CachedAt}, expires after {CacheDuration}", 
                        symbol, cachedData.CachedAt, cachedData.CacheDuration);
                }
                else
                {
                    _logger.LogInformation("Cache miss for symbol {Symbol}", symbol);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cached data for symbol {Symbol}", symbol);
                return null;
            }
        }

        public async Task<StockData> SaveStockDataAsync(StockData stockData)
        {
            try
            {
                stockData.CachedAt = DateTime.UtcNow;
                
                _context.StockData.Add(stockData);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Stock data cached for symbol {Symbol} at {CachedAt}", 
                    stockData.Symbol, stockData.CachedAt);

                return stockData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching stock data for symbol {Symbol}", stockData.Symbol);
                throw;
            }
        }

        public async Task<List<StockData>> GetRecentStockDataAsync(string symbol, int days = 7)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-days);
                
                return await _context.StockData
                    .Where(sd => sd.Symbol.ToUpper() == symbol.ToUpper() && sd.CachedAt >= cutoffDate)
                    .OrderByDescending(sd => sd.CachedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent stock data for symbol {Symbol}", symbol);
                return new List<StockData>();
            }
        }

        public async Task CleanupExpiredCacheAsync()
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-30); // Keep data for 30 days max
                
                var expiredData = await _context.StockData
                    .Where(sd => sd.CachedAt < cutoffDate)
                    .ToListAsync();

                if (expiredData.Any())
                {
                    _context.StockData.RemoveRange(expiredData);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Cleaned up {Count} expired cache entries", expiredData.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired cache entries");
            }
        }
    }
}