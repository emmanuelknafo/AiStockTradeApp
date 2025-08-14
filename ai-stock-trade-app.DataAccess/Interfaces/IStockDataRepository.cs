using AiStockTradeApp.Entities;

namespace AiStockTradeApp.DataAccess.Interfaces
{
    public interface IStockDataRepository
    {
        Task<StockData?> GetCachedStockDataAsync(string symbol);
        Task<StockData> SaveStockDataAsync(StockData stockData);
        Task<List<StockData>> GetRecentStockDataAsync(string symbol, int days = 7);
        Task CleanupExpiredCacheAsync();
    }
}