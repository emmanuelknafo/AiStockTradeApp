using ai_stock_trade_app.Models;

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
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (data?.ContainsKey("Error Message") == true)
            {
                throw new InvalidOperationException(data["Error Message"].ToString());
            }

            if (data?.ContainsKey("Note") == true)
            {
                throw new InvalidOperationException("API rate limit exceeded");
            }

            if (data?.ContainsKey("Global Quote") == true)
            {
                var quoteData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                    data["Global Quote"].ToString() ?? "{}");

                if (quoteData?.ContainsKey("05. price") == true)
                {
                    var price = decimal.Parse(quoteData["05. price"]);
                    var change = decimal.Parse(quoteData["09. change"]);
                    var percent = quoteData["10. change percent"];

                    return new StockQuoteResponse
                    {
                        Success = true,
                        Data = new StockData
                        {
                            Symbol = symbol.ToUpper(),
                            Price = price,
                            Change = change,
                            PercentChange = percent,
                            LastUpdated = DateTime.UtcNow,
                            CompanyName = symbol.ToUpper()
                        }
                    };
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
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (data?.ContainsKey("chart") == true)
            {
                var chartData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                    data["chart"].ToString() ?? "{}");

                if (chartData?.ContainsKey("result") == true)
                {
                    var resultArray = System.Text.Json.JsonSerializer.Deserialize<object[]>(
                        chartData["result"].ToString() ?? "[]");

                    if (resultArray?.Length > 0)
                    {
                        var result = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                            resultArray[0].ToString() ?? "{}");

                        if (result?.ContainsKey("meta") == true)
                        {
                            var meta = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                                result["meta"].ToString() ?? "{}");

                            if (meta?.ContainsKey("regularMarketPrice") == true && 
                                meta?.ContainsKey("previousClose") == true)
                            {
                                var price = Convert.ToDecimal(meta["regularMarketPrice"]);
                                var previousClose = Convert.ToDecimal(meta["previousClose"]);
                                var change = price - previousClose;
                                var percent = $"{(change / previousClose * 100):F2}%";

                                return new StockQuoteResponse
                                {
                                    Success = true,
                                    Data = new StockData
                                    {
                                        Symbol = symbol.ToUpper(),
                                        Price = Math.Round(price, 2),
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
            }

            throw new InvalidOperationException("Invalid response format from Yahoo Finance");
        }

        private async Task<StockQuoteResponse> FetchFromTwelveDataAsync(string symbol)
        {
            var url = $"https://api.twelvedata.com/quote?symbol={symbol}&apikey=demo";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (data?.ContainsKey("status") == true && data["status"].ToString() == "error")
            {
                throw new InvalidOperationException(data.ContainsKey("message") ? data["message"].ToString() : "Unknown error");
            }

            if (data?.ContainsKey("close") == true)
            {
                var price = Convert.ToDecimal(data["close"]);
                var change = data.ContainsKey("change") ? Convert.ToDecimal(data["change"]) : 0;
                var percent = data.ContainsKey("percent_change") ? $"{data["percent_change"]}%" : "0.00%";

                return new StockQuoteResponse
                {
                    Success = true,
                    Data = new StockData
                    {
                        Symbol = symbol.ToUpper(),
                        Price = price,
                        Change = change,
                        PercentChange = percent,
                        LastUpdated = DateTime.UtcNow,
                        CompanyName = symbol.ToUpper()
                    }
                };
            }

            throw new InvalidOperationException("Invalid response format from Twelve Data");
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
