using AiStockTradeApp.Entities;

namespace AiStockTradeApp.DataAccess.Interfaces
{
    public interface IHistoricalPriceRepository
    {
        Task UpsertAsync(HistoricalPrice price);
        Task UpsertManyAsync(IEnumerable<HistoricalPrice> prices);
        Task<List<HistoricalPrice>> GetAsync(string symbol, DateTime? from = null, DateTime? to = null, int? take = null);
        Task DeleteBySymbolAsync(string symbol);
    }
}
