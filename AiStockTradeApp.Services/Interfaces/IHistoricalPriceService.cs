using AiStockTradeApp.Entities;

namespace AiStockTradeApp.Services.Interfaces
{
    public interface IHistoricalPriceService
    {
        Task<List<HistoricalPrice>> GetAsync(string symbol, DateTime? from = null, DateTime? to = null, int? take = null);
        Task UpsertManyAsync(IEnumerable<HistoricalPrice> prices);
        Task ImportCsvAsync(string symbol, string csvContent, string? sourceName = null);
    Task<long> CountAsync(string? symbol = null);
        Task DeleteBySymbolAsync(string symbol);
    }
}
