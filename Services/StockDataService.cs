using ai_stock_trade_app.Models;
using System.Text.Json;

namespace ai_stock_trade_app.Services
{
    public interface IStockDataService
    {
        Task<StockQuoteResponse> GetStockQuoteAsync(string symbol);
        Task<List<string>> GetStockSuggestionsAsync(string query);
    }

    public class StockDataService : IStockDataService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StockDataService> _logger;
        private static readonly Dictionary<string, DateTime> _lastRequestTimes = new();
        private static readonly TimeSpan _rateLimitDelay = TimeSpan.FromSeconds(1);

        public StockDataService(HttpClient httpClient, IConfiguration configuration, ILogger<StockDataService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<StockQuoteResponse> GetStockQuoteAsync(string symbol)
        {
            try
            {
                await ApplyRateLimitAsync();

                var apiKey = _configuration["AlphaVantage:ApiKey"];
                
                // Try Alpha Vantage first if API key is available
                if (!string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_ALPHA_VANTAGE_API_KEY")
                {
                    try
                    {
                        return await FetchFromAlphaVantageAsync(symbol, apiKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Alpha Vantage failed for {Symbol}, trying backup APIs", symbol);
                    }
                }

                // Fallback to Yahoo Finance
                try
                {
                    return await FetchFromYahooFinanceAsync(symbol);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Yahoo Finance failed for {Symbol}, trying Twelve Data", symbol);
                }

                // Fallback to Twelve Data
                try
                {
                    return await FetchFromTwelveDataAsync(symbol);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "All APIs failed for {Symbol}", symbol);
                }

                return new StockQuoteResponse
                {
                    Success = false,
                    ErrorMessage = $"Unable to fetch data for {symbol} from any source"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStockQuoteAsync for {Symbol}", symbol);
                return new StockQuoteResponse
                {
                    Success = false,
                    ErrorMessage = $"Service error: {ex.Message}"
                };
            }
        }

        private async Task<StockQuoteResponse> FetchFromAlphaVantageAsync(string symbol, string apiKey)
        {
            var url = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={symbol}&apikey={apiKey}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("Error Message", out var errorElement))
            {
                throw new InvalidOperationException(errorElement.GetString() ?? "Unknown error");
            }

            if (root.TryGetProperty("Note", out var noteElement))
            {
                throw new InvalidOperationException("API rate limit exceeded");
            }

            if (root.TryGetProperty("Global Quote", out var quoteElement))
            {
                if (quoteElement.TryGetProperty("05. price", out var priceElement) &&
                    quoteElement.TryGetProperty("09. change", out var changeElement) &&
                    quoteElement.TryGetProperty("10. change percent", out var percentElement))
                {
                    if (decimal.TryParse(priceElement.GetString(), out var price) &&
                        decimal.TryParse(changeElement.GetString(), out var change))
                    {
                        return new StockQuoteResponse
                        {
                            Success = true,
                            Data = new StockData
                            {
                                Symbol = symbol.ToUpper(),
                                Price = price,
                                Change = change,
                                PercentChange = percentElement.GetString() ?? "0.00%",
                                LastUpdated = DateTime.UtcNow,
                                CompanyName = symbol.ToUpper()
                            }
                        };
                    }
                }
            }

            throw new InvalidOperationException("Invalid response format from Alpha Vantage");
        }

        private async Task<StockQuoteResponse> FetchFromYahooFinanceAsync(string symbol)
        {
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("chart", out var chartElement) &&
                chartElement.TryGetProperty("result", out var resultElement) &&
                resultElement.GetArrayLength() > 0)
            {
                var result = resultElement[0];
                if (result.TryGetProperty("meta", out var metaElement))
                {
                    if (metaElement.TryGetProperty("regularMarketPrice", out var priceElement) &&
                        metaElement.TryGetProperty("previousClose", out var previousCloseElement))
                    {
                        var price = GetDecimalFromJsonElement(priceElement);
                        var previousClose = GetDecimalFromJsonElement(previousCloseElement);
                        
                        if (price.HasValue && previousClose.HasValue)
                        {
                            var change = price.Value - previousClose.Value;
                            var percent = $"{(change / previousClose.Value * 100):F2}%";

                            return new StockQuoteResponse
                            {
                                Success = true,
                                Data = new StockData
                                {
                                    Symbol = symbol.ToUpper(),
                                    Price = Math.Round(price.Value, 2),
                                    Change = Math.Round(change, 2),
                                    PercentChange = percent,
                                    LastUpdated = DateTime.UtcNow,
                                    CompanyName = symbol.ToUpper()
                                }
                            };
                        }
                    }
                }
            }

            throw new InvalidOperationException("Invalid response format from Yahoo Finance");
        }

        private async Task<StockQuoteResponse> FetchFromTwelveDataAsync(string symbol)
        {
            var apiKey = _configuration["TwelveData:ApiKey"];
            
            // Use demo key as fallback if no API key is configured
            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_TWELVE_DATA_API_KEY")
            {
                apiKey = "demo";
            }

            var url = $"https://api.twelvedata.com/quote?symbol={symbol}&apikey={apiKey}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("status", out var statusElement) && 
                statusElement.GetString() == "error")
            {
                var message = root.TryGetProperty("message", out var messageElement) 
                    ? messageElement.GetString() 
                    : "Unknown error";
                throw new InvalidOperationException(message ?? "Unknown error");
            }

            if (root.TryGetProperty("close", out var closeElement))
            {
                var price = GetDecimalFromJsonElement(closeElement);
                var change = root.TryGetProperty("change", out var changeElement) 
                    ? GetDecimalFromJsonElement(changeElement) ?? 0 
                    : 0;
                var percent = root.TryGetProperty("percent_change", out var percentElement) 
                    ? $"{GetDecimalFromJsonElement(percentElement) ?? 0:F2}%" 
                    : "0.00%";

                if (price.HasValue)
                {
                    return new StockQuoteResponse
                    {
                        Success = true,
                        Data = new StockData
                        {
                            Symbol = symbol.ToUpper(),
                            Price = price.Value,
                            Change = change,
                            PercentChange = percent,
                            LastUpdated = DateTime.UtcNow,
                            CompanyName = symbol.ToUpper()
                        }
                    };
                }
            }

            throw new InvalidOperationException("Invalid response format from Twelve Data");
        }

        private static decimal? GetDecimalFromJsonElement(JsonElement element)
        {
            try
            {
                return element.ValueKind switch
                {
                    JsonValueKind.Number => element.GetDecimal(),
                    JsonValueKind.String => decimal.TryParse(element.GetString(), out var result) ? result : null,
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        private static async Task ApplyRateLimitAsync()
        {
            var key = "general";
            var now = DateTime.UtcNow;

            if (_lastRequestTimes.ContainsKey(key))
            {
                var timeSinceLastRequest = now - _lastRequestTimes[key];
                if (timeSinceLastRequest < _rateLimitDelay)
                {
                    var delay = _rateLimitDelay - timeSinceLastRequest;
                    await Task.Delay(delay);
                }
            }

            _lastRequestTimes[key] = DateTime.UtcNow;
        }

        public Task<List<string>> GetStockSuggestionsAsync(string query)
        {
            // Popular stock symbols for suggestions (same as in JS app)
            var popularStocks = new[]
            {
                "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", "NVDA", "META", "NFLX",
                "DIS", "BABA", "V", "JPM", "JNJ", "WMT", "PG", "UNH", "HD", "MA",
                "PYPL", "ADBE", "CRM", "INTC", "CSCO", "PFE", "VZ", "KO", "PEP",
                "T", "XOM", "CVX", "BAC", "WFC", "C", "GS", "MS"
            };

            var result = popularStocks
                .Where(stock => stock.Contains(query.ToUpper()))
                .Take(8)
                .ToList();

            return Task.FromResult(result);
        }
    }
}
