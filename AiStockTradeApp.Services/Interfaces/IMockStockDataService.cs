using AiStockTradeApp.Entities;

namespace AiStockTradeApp.Services.Interfaces;

/// <summary>
/// Interface for mock stock data service to enable testing
/// </summary>
public interface IMockStockDataService
{
    /// <summary>
    /// Generates mock stock data for a given symbol
    /// </summary>
    /// <param name="symbol">Stock symbol</param>
    /// <returns>Mock stock data</returns>
    StockQuoteResponse GenerateMockStockData(string symbol);

    /// <summary>
    /// Generates mock historical data for a given symbol
    /// </summary>
    /// <param name="symbol">Stock symbol</param>
    /// <param name="days">Number of days of historical data</param>
    /// <returns>List of mock historical prices</returns>
    List<HistoricalPrice> GenerateMockHistoricalData(string symbol, int days);

    /// <summary>
    /// Gets mock stock suggestions based on query
    /// </summary>
    /// <param name="query">Search query</param>
    /// <returns>List of mock stock suggestions</returns>
    List<StockQuoteResponse> GetMockStockSuggestions(string query);
}
