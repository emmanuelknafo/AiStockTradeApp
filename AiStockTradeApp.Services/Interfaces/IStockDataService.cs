using AiStockTradeApp.Entities;

namespace AiStockTradeApp.Services.Interfaces
{
    public interface IStockDataService
    {
        Task<StockQuoteResponse> GetStockQuoteAsync(string symbol);
        Task<List<string>> GetStockSuggestionsAsync(string query);
        Task<List<ChartDataPoint>> GetHistoricalDataAsync(string symbol, int days = 30);
    }
}