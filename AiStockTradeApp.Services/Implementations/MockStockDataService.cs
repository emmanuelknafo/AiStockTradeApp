using AiStockTradeApp.Entities;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AiStockTradeApp.Services.Implementations;

/// <summary>
/// Mock stock data service that provides realistic test data when external APIs are unavailable
/// Used for testing and development when API credits are exhausted
/// </summary>
public class MockStockDataService : IMockStockDataService
{
    private readonly ILogger<MockStockDataService> _logger;
    private readonly Random _random = new();
    
    // Base prices for common stocks to make mock data more realistic
    private readonly Dictionary<string, decimal> _basePrices = new()
    {
        ["AAPL"] = 170.50m,
        ["GOOGL"] = 130.25m,
        ["MSFT"] = 280.75m,
        ["AMZN"] = 95.30m,
        ["TSLA"] = 230.40m,
        ["META"] = 295.80m,
        ["NVDA"] = 415.20m,
        ["NFLX"] = 385.60m,
        ["AMD"] = 105.45m,
        ["INTC"] = 25.80m,
        ["CRM"] = 215.30m,
        ["ORCL"] = 108.90m,
        ["IBM"] = 145.60m,
        ["CSCO"] = 47.25m,
        ["ADBE"] = 485.70m
    };

    public MockStockDataService(ILogger<MockStockDataService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates realistic mock stock data for testing purposes
    /// </summary>
    public StockQuoteResponse GenerateMockStockData(string symbol)
    {
        try
        {
            _logger.LogInformation("Generating mock data for symbol {Symbol}", symbol);

            // Get base price or generate one for unknown symbols
            var basePrice = _basePrices.ContainsKey(symbol.ToUpper()) 
                ? _basePrices[symbol.ToUpper()] 
                : (decimal)(_random.NextDouble() * 200 + 50); // Random price between $50-$250

            // Generate realistic price variation (-5% to +5%)
            var variation = (decimal)((_random.NextDouble() - 0.5) * 0.1); // -5% to +5%
            var currentPrice = Math.Round(basePrice * (1 + variation), 2);
            
            // Calculate change from previous day (simulate previous close)
            var previousClose = Math.Round(basePrice * (1 + (decimal)((_random.NextDouble() - 0.5) * 0.08)), 2);
            var change = currentPrice - previousClose;
            var changePercent = previousClose != 0 ? Math.Round((change / previousClose) * 100, 2) : 0;

            var stockData = new StockData
            {
                Symbol = symbol.ToUpper(),
                CompanyName = GetMockCompanyName(symbol),
                Price = currentPrice,
                Change = change,
                PercentChange = $"{changePercent:F2}%",
                LastUpdated = DateTime.UtcNow,
                Currency = "USD",
                AIAnalysis = GenerateAIAnalysis(symbol, changePercent),
                Recommendation = changePercent > 0 ? "BUY" : changePercent < -2 ? "SELL" : "HOLD",
                RecommendationReason = $"Based on {Math.Abs(changePercent):F2}% price movement and market conditions",
                CachedAt = DateTime.UtcNow,
                CacheDuration = TimeSpan.FromMinutes(5) // Shorter cache for mock data to encourage retrying real APIs
            };

            return new StockQuoteResponse
            {
                Success = true,
                Data = stockData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating mock data for {Symbol}", symbol);
            return new StockQuoteResponse
            {
                Success = false,
                ErrorMessage = $"Failed to generate mock data for {symbol}"
            };
        }
    }

    /// <summary>
    /// Generates realistic historical price data for testing
    /// </summary>
    public List<HistoricalPrice> GenerateMockHistoricalData(string symbol, int days = 30)
    {
        try
        {
            _logger.LogInformation("Generating mock historical data for {Symbol} ({Days} days)", symbol, days);

            var basePrice = _basePrices.ContainsKey(symbol.ToUpper()) 
                ? _basePrices[symbol.ToUpper()] 
                : (decimal)(_random.NextDouble() * 200 + 50);

            var historicalData = new List<HistoricalPrice>();
            var currentPrice = basePrice;

            for (int i = days; i >= 0; i--)
            {
                var date = DateTime.UtcNow.Date.AddDays(-i);
                
                // Generate realistic price movement (max 5% daily change)
                var dailyChange = (decimal)((_random.NextDouble() - 0.5) * 0.1); // -5% to +5%
                currentPrice = Math.Round(currentPrice * (1 + dailyChange), 2);
                
                // Ensure price doesn't go below $1
                if (currentPrice < 1) currentPrice = 1.00m;

                // Generate OHLC data with realistic relationships
                var open = Math.Round(currentPrice * (1 + (decimal)((_random.NextDouble() - 0.5) * 0.02)), 2);
                var high = Math.Round(Math.Max(open, currentPrice) * (1 + (decimal)(_random.NextDouble() * 0.03)), 2);
                var low = Math.Round(Math.Min(open, currentPrice) * (1 - (decimal)(_random.NextDouble() * 0.03)), 2);

                historicalData.Add(new HistoricalPrice
                {
                    Symbol = symbol.ToUpper(),
                    Date = date,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = currentPrice,
                    Volume = _random.Next(500000, 20000000) // Random volume
                });
            }

            return historicalData.OrderBy(h => h.Date).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating mock historical data for {Symbol}", symbol);
            return new List<HistoricalPrice>();
        }
    }

    private string GenerateAIAnalysis(string symbol, decimal changePercent)
    {
        var direction = changePercent > 0 ? "upward" : "downward";
        var strength = Math.Abs(changePercent) > 3 ? "strong" : Math.Abs(changePercent) > 1 ? "moderate" : "slight";
        var outlook = changePercent > 2 ? "bullish" : changePercent < -2 ? "bearish" : "neutral";
        
        return $"Stock {symbol} shows a {strength} {direction} trend with {Math.Abs(changePercent):F2}% movement. " +
               $"Technical indicators suggest a {outlook} outlook in the short term. " +
               $"Mock analysis generated due to API limitations - consider fundamental analysis for real trading decisions.";
    }

    private string GetMockCompanyName(string symbol)
    {
        var companyNames = new Dictionary<string, string>
        {
            ["AAPL"] = "Apple Inc.",
            ["GOOGL"] = "Alphabet Inc.",
            ["MSFT"] = "Microsoft Corporation",
            ["AMZN"] = "Amazon.com Inc.",
            ["TSLA"] = "Tesla Inc.",
            ["META"] = "Meta Platforms Inc.",
            ["NVDA"] = "NVIDIA Corporation",
            ["NFLX"] = "Netflix Inc.",
            ["AMD"] = "Advanced Micro Devices Inc.",
            ["INTC"] = "Intel Corporation",
            ["CRM"] = "Salesforce Inc.",
            ["ORCL"] = "Oracle Corporation",
            ["IBM"] = "International Business Machines",
            ["CSCO"] = "Cisco Systems Inc.",
            ["ADBE"] = "Adobe Inc."
        };

        return companyNames.ContainsKey(symbol.ToUpper()) 
            ? companyNames[symbol.ToUpper()] 
            : $"{symbol.ToUpper()} Corporation";
    }

    /// <summary>
    /// Gets mock stock suggestions based on query
    /// </summary>
    /// <param name="query">Search query</param>
    /// <returns>List of mock stock suggestions</returns>
    public List<StockQuoteResponse> GetMockStockSuggestions(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<StockQuoteResponse>();
        }

        var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "META", "NVDA", "NFLX", "AMD", "INTC" };
        var matchingSymbols = symbols
            .Where(s => s.StartsWith(query.ToUpper(), StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        return matchingSymbols.Select(GenerateMockStockData).ToList();
    }
}
