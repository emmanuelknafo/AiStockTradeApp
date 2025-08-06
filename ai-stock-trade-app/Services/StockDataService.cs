using ai_stock_trade_app.Models;
using System.Text.Json;

namespace ai_stock_trade_app.Services
{
    public interface IStockDataService
    {
        Task<StockQuoteResponse> GetStockQuoteAsync(string symbol);
        Task<List<string>> GetStockSuggestionsAsync(string query);
        Task<List<ChartDataPoint>> GetHistoricalDataAsync(string symbol, int days = 30);
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
            // Handle null or empty query
            if (string.IsNullOrWhiteSpace(query))
            {
                return Task.FromResult(new List<string>());
            }

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

        public async Task<List<ChartDataPoint>> GetHistoricalDataAsync(string symbol, int days = 30)
        {
            try
            {
                _logger.LogInformation("Fetching historical data for {Symbol}, days: {Days}", symbol, days);
                await ApplyRateLimitAsync();

                // Try Alpha Vantage first if API key is available
                var apiKey = _configuration["AlphaVantage:ApiKey"];
                _logger.LogInformation("Alpha Vantage API key configured: {HasKey}", !string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_ALPHA_VANTAGE_API_KEY");
                
                if (!string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_ALPHA_VANTAGE_API_KEY")
                {
                    try
                    {
                        _logger.LogInformation("Attempting to fetch from Alpha Vantage for {Symbol}", symbol);
                        var result = await FetchHistoricalFromAlphaVantageAsync(symbol, apiKey, days);
                        _logger.LogInformation("Alpha Vantage returned {Count} data points for {Symbol}", result.Count, symbol);
                        if (result.Count > 0)
                        {
                            return result;
                        }
                        _logger.LogWarning("Alpha Vantage returned no data for {Symbol}, using fallback", symbol);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Alpha Vantage historical data failed for {Symbol}, using fallback", symbol);
                    }
                }

                // Fallback to generated demo data
                _logger.LogInformation("Generating demo chart data for {Symbol}", symbol);
                var demoData = GenerateDemoChartData(symbol, days);
                _logger.LogInformation("Generated {Count} demo data points for {Symbol}", demoData.Count, symbol);
                return demoData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching historical data for {Symbol}", symbol);
                var fallbackData = GenerateDemoChartData(symbol, days);
                _logger.LogInformation("Generated {Count} fallback data points for {Symbol}", fallbackData.Count, symbol);
                return fallbackData;
            }
        }

        private async Task<List<ChartDataPoint>> FetchHistoricalFromAlphaVantageAsync(string symbol, string apiKey, int days)
        {
            var url = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={symbol}&apikey={apiKey}&outputsize=compact";
            _logger.LogInformation("Calling Alpha Vantage API: {Url}", url.Replace(apiKey, "***"));
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Alpha Vantage response length: {Length}", json.Length);
            _logger.LogDebug("Alpha Vantage raw response: {Response}", json.Length > 1000 ? json.Substring(0, 1000) + "..." : json);
            
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var chartData = new List<ChartDataPoint>();

            if (root.TryGetProperty("Time Series (Daily)", out var timeSeriesElement))
            {
                _logger.LogInformation("Found Time Series (Daily) data for {Symbol}", symbol);
                var count = 0;
                foreach (var dateEntry in timeSeriesElement.EnumerateObject().OrderByDescending(x => x.Name))
                {
                    if (count >= days) break;

                    if (DateTime.TryParse(dateEntry.Name, out var date))
                    {
                        var dailyData = dateEntry.Value;
                        if (dailyData.TryGetProperty("4. close", out var closeElement) &&
                            dailyData.TryGetProperty("5. volume", out var volumeElement))
                        {
                            var price = GetDecimalFromJsonElement(closeElement);
                            var volume = GetDecimalFromJsonElement(volumeElement);

                            if (price.HasValue && volume.HasValue)
                            {
                                chartData.Add(new ChartDataPoint
                                {
                                    Date = date,
                                    Price = price.Value,
                                    Volume = volume.Value
                                });
                            }
                        }
                    }
                    count++;
                }
                _logger.LogInformation("Processed {Count} data points from Alpha Vantage for {Symbol}", chartData.Count, symbol);
            }
            else
            {
                _logger.LogWarning("No 'Time Series (Daily)' property found in Alpha Vantage response for {Symbol}", symbol);
                
                // Check for error messages
                if (root.TryGetProperty("Error Message", out var errorElement))
                {
                    _logger.LogError("Alpha Vantage error: {Error}", errorElement.GetString());
                }
                else if (root.TryGetProperty("Note", out var noteElement))
                {
                    _logger.LogWarning("Alpha Vantage note: {Note}", noteElement.GetString());
                }
            }

            var orderedData = chartData.OrderBy(x => x.Date).ToList();
            _logger.LogInformation("Returning {Count} ordered data points for {Symbol}", orderedData.Count, symbol);
            return orderedData;
        }

        private List<ChartDataPoint> GenerateDemoChartData(string symbol, int days)
        {
            // Handle null or empty symbol
            var seedSymbol = string.IsNullOrEmpty(symbol) ? "DEFAULT" : symbol;
            
            var random = new Random(seedSymbol.GetHashCode()); // Consistent seed for same symbol
            var chartData = new List<ChartDataPoint>();
            var basePrice = random.Next(50, 500); // Random base price between $50-$500
            var currentPrice = (decimal)basePrice;

            // Ensure we have at least 1 day of data and handle negative days
            var actualDays = Math.Max(1, days);

            for (int i = actualDays - 1; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                
                // Generate realistic price movement (-3% to +3% daily change)
                var changePercent = (decimal)((random.NextDouble() - 0.5) * 0.06);
                currentPrice *= (1 + changePercent);
                currentPrice = Math.Max(currentPrice, 1); // Ensure price stays positive

                var volume = random.Next(100000, 10000000); // Random volume

                chartData.Add(new ChartDataPoint
                {
                    Date = date,
                    Price = Math.Round(currentPrice, 2),
                    Volume = volume
                });
            }

            return chartData;
        }
    }
}
