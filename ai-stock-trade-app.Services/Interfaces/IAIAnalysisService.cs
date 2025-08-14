using AiStockTradeApp.Entities;

namespace AiStockTradeApp.Services.Interfaces
{
    public interface IAIAnalysisService
    {
        Task<(string analysis, string recommendation, string reasoning)> GenerateAnalysisAsync(string symbol, StockData stockData);
    }
}