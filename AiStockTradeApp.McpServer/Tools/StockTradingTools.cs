using System.ComponentModel;
using System.Text.Json;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Entities.ViewModels;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.ApplicationInsights;
using System.Diagnostics;

/// <summary>
/// MCP tools for stock trading operations that interface with the AI Stock Trade API.
/// These tools provide access to real-time stock quotes, historical data, stock analysis, and trading operations.
/// </summary>
internal class StockTradingTools
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StockTradingTools> _logger;
    private readonly TelemetryClient _telemetryClient;
    private readonly string _apiBaseUrl;

    public StockTradingTools(HttpClient httpClient, ILogger<StockTradingTools> logger, TelemetryClient telemetryClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _telemetryClient = telemetryClient;
        _apiBaseUrl = configuration["STOCK_API_BASE_URL"] ?? 
                     Environment.GetEnvironmentVariable("STOCK_API_BASE_URL") ?? 
                     "http://localhost:5000";
        
        _httpClient.BaseAddress = new Uri(_apiBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        _logger.LogInformation("StockTradingTools initialized with API base URL: {ApiBaseUrl}", _apiBaseUrl);
    }

    [McpServerTool]
    [Description("Get real-time stock quote data including current price, change, and basic company information.")]
    public async Task<string> GetStockQuote(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetStockQuote");
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("GetStockQuote started for symbol: {Symbol}", symbol);
            
            if (string.IsNullOrWhiteSpace(symbol))
            {
                _logger.LogWarning("GetStockQuote called with empty or null symbol");
                _telemetryClient.TrackEvent("StockQuote.ValidationError", new Dictionary<string, string>
                {
                    ["Error"] = "EmptySymbol",
                    ["Symbol"] = symbol ?? "null"
                });
                return JsonSerializer.Serialize(new { error = "Stock symbol is required" });
            }

            symbol = symbol.ToUpperInvariant().Trim();
            activity?.SetTag("stock.symbol", symbol);
            
            _logger.LogDebug("Making API request for stock quote: {Symbol} to {ApiUrl}", symbol, $"{_apiBaseUrl}/api/stocks/quote?symbol={symbol}");
            
            var response = await _httpClient.GetAsync($"/api/stocks/quote?symbol={symbol}");
            
            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;
            
            _telemetryClient.TrackDependency("HTTP", "StockAPI", $"GET /api/stocks/quote?symbol={symbol}", DateTime.UtcNow.Subtract(stopwatch.Elapsed), stopwatch.Elapsed, response.IsSuccessStatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("API response received for {Symbol}, content length: {ContentLength}", symbol, content.Length);
                
                var stockData = JsonSerializer.Deserialize<StockData>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                _logger.LogInformation("GetStockQuote completed successfully for {Symbol} - Price: {Price}, Change: {Change}%, Duration: {Duration}ms", 
                    symbol, stockData?.Price.ToString() ?? "null", stockData?.PercentChange ?? "null", duration);

                _telemetryClient.TrackEvent("StockQuote.Success", new Dictionary<string, string>
                {
                    ["Symbol"] = symbol,
                    ["Price"] = stockData?.Price.ToString() ?? "null",
                    ["Change"] = stockData?.PercentChange ?? "null",
                    ["CompanyName"] = stockData?.CompanyName ?? "null"
                });

                _telemetryClient.TrackMetric("StockQuote.ResponseTime", duration, new Dictionary<string, string>
                {
                    ["Symbol"] = symbol,
                    ["Success"] = "true"
                });

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    data = new
                    {
                        symbol = stockData?.Symbol,
                        price = stockData?.Price,
                        change = stockData?.Change,
                        percentChange = stockData?.PercentChange,
                        companyName = stockData?.CompanyName,
                        currency = stockData?.Currency,
                        lastUpdated = stockData?.LastUpdated,
                        aiAnalysis = stockData?.AIAnalysis
                    }
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"API returned {response.StatusCode}: {errorContent}";
                
                _logger.LogWarning("GetStockQuote failed for {Symbol} - StatusCode: {StatusCode}, Error: {Error}, Duration: {Duration}ms", 
                    symbol, response.StatusCode, errorContent, duration);

                _telemetryClient.TrackEvent("StockQuote.ApiError", new Dictionary<string, string>
                {
                    ["Symbol"] = symbol,
                    ["StatusCode"] = response.StatusCode.ToString(),
                    ["Error"] = errorContent
                });

                _telemetryClient.TrackMetric("StockQuote.ResponseTime", duration, new Dictionary<string, string>
                {
                    ["Symbol"] = symbol,
                    ["Success"] = "false",
                    ["StatusCode"] = response.StatusCode.ToString()
                });
                
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = errorMessage
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;
            
            _logger.LogError(ex, "GetStockQuote exception for {Symbol} - Duration: {Duration}ms", symbol, duration);
            
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Method"] = "GetStockQuote",
                ["Symbol"] = symbol ?? "null",
                ["Duration"] = duration.ToString()
            });

            _telemetryClient.TrackEvent("StockQuote.Exception", new Dictionary<string, string>
            {
                ["Symbol"] = symbol ?? "null",
                ["ExceptionType"] = ex.GetType().Name,
                ["Message"] = ex.Message
            });
            
            return JsonSerializer.Serialize(new 
            { 
                success = false, 
                error = $"Failed to get stock quote: {ex.Message}" 
            });
        }
    }

    [McpServerTool]
    [Description("Get historical price data for a stock over a specified number of days. Returns chart-ready data points.")]
    public async Task<string> GetHistoricalData(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol,
        [Description("Number of days of historical data to retrieve (default: 30, max: 365)")] int days = 30)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetHistoricalData");
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("GetHistoricalData started for symbol: {Symbol}, days: {Days}", symbol, days);
            
            if (string.IsNullOrWhiteSpace(symbol))
            {
                _logger.LogWarning("GetHistoricalData called with empty or null symbol");
                _telemetryClient.TrackEvent("HistoricalData.ValidationError", new Dictionary<string, string>
                {
                    ["Error"] = "EmptySymbol",
                    ["Symbol"] = symbol ?? "null",
                    ["Days"] = days.ToString()
                });
                return JsonSerializer.Serialize(new { error = "Stock symbol is required" });
            }

            symbol = symbol.ToUpperInvariant().Trim();
            days = Math.Max(1, Math.Min(365, days)); // Clamp between 1 and 365 days
            
            activity?.SetTag("stock.symbol", symbol);
            activity?.SetTag("stock.days", days);

            _logger.LogDebug("Making API request for historical data: {Symbol}, {Days} days to {ApiUrl}", 
                symbol, days, $"{_apiBaseUrl}/api/stocks/historical?symbol={symbol}&days={days}");

            var response = await _httpClient.GetAsync($"/api/stocks/historical?symbol={symbol}&days={days}");
            
            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;
            
            _telemetryClient.TrackDependency("HTTP", "StockAPI", $"GET /api/stocks/historical?symbol={symbol}&days={days}", 
                DateTime.UtcNow.Subtract(stopwatch.Elapsed), stopwatch.Elapsed, response.IsSuccessStatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("API response received for {Symbol} historical data, content length: {ContentLength}", symbol, content.Length);
                
                var chartData = JsonSerializer.Deserialize<List<ChartDataPoint>>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                var dataPointsCount = chartData?.Count ?? 0;
                
                _logger.LogInformation("GetHistoricalData completed successfully for {Symbol} - Days requested: {DaysRequested}, Data points returned: {DataPoints}, Duration: {Duration}ms", 
                    symbol, days, dataPointsCount, duration);

                _telemetryClient.TrackEvent("HistoricalData.Success", new Dictionary<string, string>
                {
                    ["Symbol"] = symbol,
                    ["DaysRequested"] = days.ToString(),
                    ["DataPointsReturned"] = dataPointsCount.ToString()
                });

                _telemetryClient.TrackMetric("HistoricalData.ResponseTime", duration, new Dictionary<string, string>
                {
                    ["Symbol"] = symbol,
                    ["Success"] = "true",
                    ["DaysRequested"] = days.ToString()
                });

                _telemetryClient.TrackMetric("HistoricalData.DataPoints", dataPointsCount, new Dictionary<string, string>
                {
                    ["Symbol"] = symbol,
                    ["DaysRequested"] = days.ToString()
                });

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    symbol = symbol,
                    daysRequested = days,
                    dataPointsReturned = dataPointsCount,
                    data = chartData
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"API returned {response.StatusCode}: {errorContent}";
                
                _logger.LogWarning("GetHistoricalData failed for {Symbol} - StatusCode: {StatusCode}, Error: {Error}, Duration: {Duration}ms", 
                    symbol, response.StatusCode, errorContent, duration);

                _telemetryClient.TrackEvent("HistoricalData.ApiError", new Dictionary<string, string>
                {
                    ["Symbol"] = symbol,
                    ["Days"] = days.ToString(),
                    ["StatusCode"] = response.StatusCode.ToString(),
                    ["Error"] = errorContent
                });

                _telemetryClient.TrackMetric("HistoricalData.ResponseTime", duration, new Dictionary<string, string>
                {
                    ["Symbol"] = symbol,
                    ["Success"] = "false",
                    ["StatusCode"] = response.StatusCode.ToString()
                });
                
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = errorMessage
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;
            
            _logger.LogError(ex, "GetHistoricalData exception for {Symbol}, days: {Days} - Duration: {Duration}ms", symbol, days, duration);
            
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Method"] = "GetHistoricalData",
                ["Symbol"] = symbol ?? "null",
                ["Days"] = days.ToString(),
                ["Duration"] = duration.ToString()
            });

            _telemetryClient.TrackEvent("HistoricalData.Exception", new Dictionary<string, string>
            {
                ["Symbol"] = symbol ?? "null",
                ["Days"] = days.ToString(),
                ["ExceptionType"] = ex.GetType().Name,
                ["Message"] = ex.Message
            });
            
            return JsonSerializer.Serialize(new 
            { 
                success = false, 
                error = $"Failed to get historical data: {ex.Message}" 
            });
        }
    }

    [McpServerTool]
    [Description("Search for stock symbols based on a query string. Useful for finding the correct symbol for a company.")]
    public async Task<string> SearchStockSymbols(
        [Description("Search query (company name, partial symbol, etc.)")] string query)
    {
        using var activity = Activity.Current?.Source.StartActivity("SearchStockSymbols");
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("SearchStockSymbols started for query: {Query}", query);
            
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("SearchStockSymbols called with empty or null query");
                _telemetryClient.TrackEvent("StockSearch.ValidationError", new Dictionary<string, string>
                {
                    ["Error"] = "EmptyQuery",
                    ["Query"] = query ?? "null"
                });
                return JsonSerializer.Serialize(new { error = "Search query is required" });
            }

            activity?.SetTag("search.query", query);
            
            _logger.LogDebug("Making API request for stock search: {Query} to {ApiUrl}", 
                query, $"{_apiBaseUrl}/api/stocks/suggestions?query={Uri.EscapeDataString(query)}");

            var response = await _httpClient.GetAsync($"/api/stocks/suggestions?query={Uri.EscapeDataString(query)}");
            
            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;
            
            _telemetryClient.TrackDependency("HTTP", "StockAPI", $"GET /api/stocks/suggestions?query={Uri.EscapeDataString(query)}", 
                DateTime.UtcNow.Subtract(stopwatch.Elapsed), stopwatch.Elapsed, response.IsSuccessStatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("API response received for search query {Query}, content length: {ContentLength}", query, content.Length);
                
                var suggestions = JsonSerializer.Deserialize<List<string>>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                var suggestionsCount = suggestions?.Count ?? 0;
                
                _logger.LogInformation("SearchStockSymbols completed successfully for query: {Query} - Suggestions returned: {SuggestionsCount}, Duration: {Duration}ms", 
                    query, suggestionsCount, duration);

                _telemetryClient.TrackEvent("StockSearch.Success", new Dictionary<string, string>
                {
                    ["Query"] = query,
                    ["SuggestionsCount"] = suggestionsCount.ToString()
                });

                _telemetryClient.TrackMetric("StockSearch.ResponseTime", duration, new Dictionary<string, string>
                {
                    ["Success"] = "true",
                    ["QueryLength"] = query.Length.ToString()
                });

                _telemetryClient.TrackMetric("StockSearch.Results", suggestionsCount, new Dictionary<string, string>
                {
                    ["Query"] = query,
                    ["QueryLength"] = query.Length.ToString()
                });

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    query = query,
                    suggestions = suggestions
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"API returned {response.StatusCode}: {errorContent}";
                
                _logger.LogWarning("SearchStockSymbols failed for query: {Query} - StatusCode: {StatusCode}, Error: {Error}, Duration: {Duration}ms", 
                    query, response.StatusCode, errorContent, duration);

                _telemetryClient.TrackEvent("StockSearch.ApiError", new Dictionary<string, string>
                {
                    ["Query"] = query,
                    ["StatusCode"] = response.StatusCode.ToString(),
                    ["Error"] = errorContent
                });

                _telemetryClient.TrackMetric("StockSearch.ResponseTime", duration, new Dictionary<string, string>
                {
                    ["Success"] = "false",
                    ["StatusCode"] = response.StatusCode.ToString()
                });
                
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = errorMessage
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;
            
            _logger.LogError(ex, "SearchStockSymbols exception for query: {Query} - Duration: {Duration}ms", query, duration);
            
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Method"] = "SearchStockSymbols",
                ["Query"] = query ?? "null",
                ["Duration"] = duration.ToString()
            });

            _telemetryClient.TrackEvent("StockSearch.Exception", new Dictionary<string, string>
            {
                ["Query"] = query ?? "null",
                ["ExceptionType"] = ex.GetType().Name,
                ["Message"] = ex.Message
            });
            
            return JsonSerializer.Serialize(new 
            { 
                success = false, 
                error = $"Failed to search stock symbols: {ex.Message}" 
            });
        }
    }

    [McpServerTool]
    [Description("Get detailed information about a listed stock including company name, exchange, sector, and industry.")]
    public async Task<string> GetStockDetails(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return JsonSerializer.Serialize(new { error = "Stock symbol is required" });

            symbol = symbol.ToUpperInvariant().Trim();

            var response = await _httpClient.GetAsync($"/api/listed-stocks/{symbol}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var listedStock = JsonSerializer.Deserialize<ListedStock>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    data = listedStock
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = $"Stock symbol '{symbol}' not found in listed stocks database" 
                });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = $"API returned {response.StatusCode}: {errorContent}" 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stock details for {Symbol}", symbol);
            return JsonSerializer.Serialize(new 
            { 
                success = false, 
                error = $"Failed to get stock details: {ex.Message}" 
            });
        }
    }

    [McpServerTool]
    [Description("Get a list of available listed stocks with pagination support. Useful for browsing available stocks.")]
    public async Task<string> GetListedStocks(
        [Description("Number of stocks to skip (for pagination, default: 0)")] int skip = 0,
        [Description("Number of stocks to take (default: 50, max: 500)")] int take = 50)
    {
        try
        {
            skip = Math.Max(0, skip);
            take = Math.Max(1, Math.Min(500, take)); // Clamp between 1 and 500

            var response = await _httpClient.GetAsync($"/api/listed-stocks?skip={skip}&take={take}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var stocks = JsonSerializer.Deserialize<List<ListedStock>>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                // Also get the total count
                var countResponse = await _httpClient.GetAsync("/api/listed-stocks/count");
                var totalCount = 0;
                if (countResponse.IsSuccessStatusCode)
                {
                    var countContent = await countResponse.Content.ReadAsStringAsync();
                    int.TryParse(countContent, out totalCount);
                }

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    pagination = new
                    {
                        skip = skip,
                        take = take,
                        returned = stocks?.Count ?? 0,
                        totalAvailable = totalCount
                    },
                    data = stocks
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = $"API returned {response.StatusCode}: {errorContent}" 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting listed stocks");
            return JsonSerializer.Serialize(new 
            { 
                success = false, 
                error = $"Failed to get listed stocks: {ex.Message}" 
            });
        }
    }

    [McpServerTool]
    [Description("Get a random listed stock from the available stocks. Perfect for stock discovery and exploring new investment opportunities.")]
    public async Task<string> GetRandomListedStock()
    {
        try
        {
            // First, get the total count of available stocks
            var countResponse = await _httpClient.GetAsync("/api/listed-stocks/count");
            if (!countResponse.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = "Failed to get total stock count from API" 
                });
            }

            var countContent = await countResponse.Content.ReadAsStringAsync();
            if (!int.TryParse(countContent, out var totalCount) || totalCount <= 0)
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = "No stocks available or invalid count returned" 
                });
            }

            // Generate a random index within the available range
            var random = new Random();
            var randomIndex = random.Next(0, totalCount);

            // Get one stock starting from the random index
            var response = await _httpClient.GetAsync($"/api/listed-stocks?skip={randomIndex}&take=1");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var stocks = JsonSerializer.Deserialize<List<ListedStock>>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (stocks?.Count > 0)
                {
                    var randomStock = stocks[0];
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        randomSelection = new
                        {
                            selectedFrom = totalCount,
                            randomIndex = randomIndex
                        },
                        data = new
                        {
                            symbol = randomStock.Symbol,
                            name = randomStock.Name,
                            lastSale = randomStock.LastSale,
                            netChange = randomStock.NetChange,
                            percentChange = randomStock.PercentChange,
                            marketCap = randomStock.MarketCap,
                            country = randomStock.Country,
                            ipoYear = randomStock.IpoYear,
                            volume = randomStock.Volume,
                            sector = randomStock.Sector,
                            industry = randomStock.Industry,
                            updatedAt = randomStock.UpdatedAt
                        }
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    return JsonSerializer.Serialize(new 
                    { 
                        success = false, 
                        error = "No stock found at random index" 
                    });
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = $"API returned {response.StatusCode}: {errorContent}" 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting random listed stock");
            return JsonSerializer.Serialize(new 
            { 
                success = false, 
                error = $"Failed to get random stock: {ex.Message}" 
            });
        }
    }

    [McpServerTool]
    [Description("Get detailed historical price data for a stock with optional date range filtering.")]
    public async Task<string> GetDetailedHistoricalPrices(
        [Description("Stock symbol (e.g., AAPL, MSFT, GOOGL)")] string symbol,
        [Description("Start date (YYYY-MM-DD format, optional)")] string? fromDate = null,
        [Description("End date (YYYY-MM-DD format, optional)")] string? toDate = null,
        [Description("Maximum number of records to return (optional, default: 100)")] int? take = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return JsonSerializer.Serialize(new { error = "Stock symbol is required" });

            symbol = symbol.ToUpperInvariant().Trim();

            var queryParams = new List<string> { $"symbol={symbol}" };
            
            if (!string.IsNullOrWhiteSpace(fromDate))
                queryParams.Add($"from={Uri.EscapeDataString(fromDate)}");
            
            if (!string.IsNullOrWhiteSpace(toDate))
                queryParams.Add($"to={Uri.EscapeDataString(toDate)}");
            
            if (take.HasValue && take.Value > 0)
                queryParams.Add($"take={take.Value}");

            var queryString = string.Join("&", queryParams);
            var response = await _httpClient.GetAsync($"/api/historical-prices/{symbol}?{queryString}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var historicalPrices = JsonSerializer.Deserialize<List<HistoricalPrice>>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                // Get count for this symbol
                var countResponse = await _httpClient.GetAsync($"/api/historical-prices/{symbol}/count");
                var totalCount = 0L;
                if (countResponse.IsSuccessStatusCode)
                {
                    var countContent = await countResponse.Content.ReadAsStringAsync();
                    long.TryParse(countContent, out totalCount);
                }

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    symbol = symbol,
                    filters = new
                    {
                        fromDate = fromDate,
                        toDate = toDate,
                        take = take
                    },
                    returned = historicalPrices?.Count ?? 0,
                    totalAvailableForSymbol = totalCount,
                    data = historicalPrices
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Serialize(new 
                { 
                    success = false, 
                    error = $"API returned {response.StatusCode}: {errorContent}" 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting detailed historical prices for {Symbol}", symbol);
            return JsonSerializer.Serialize(new 
            { 
                success = false, 
                error = $"Failed to get detailed historical prices: {ex.Message}" 
            });
        }
    }

    [McpServerTool]
    [Description("Get API health status and basic statistics about the stock trading system.")]
    public async Task<string> GetSystemStatus()
    {
        try
        {
            var tasks = new[]
            {
                _httpClient.GetAsync("/api/listed-stocks/count"),
                _httpClient.GetAsync("/api/historical-prices/count")
            };

            var responses = await Task.WhenAll(tasks);
            
            var listedStocksCount = 0;
            var historicalPricesCount = 0L;

            if (responses[0].IsSuccessStatusCode)
            {
                var content = await responses[0].Content.ReadAsStringAsync();
                int.TryParse(content, out listedStocksCount);
            }

            if (responses[1].IsSuccessStatusCode)
            {
                var content = await responses[1].Content.ReadAsStringAsync();
                long.TryParse(content, out historicalPricesCount);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                timestamp = DateTime.UtcNow,
                apiBaseUrl = _apiBaseUrl,
                statistics = new
                {
                    listedStocksCount = listedStocksCount,
                    historicalPricesCount = historicalPricesCount
                },
                status = "healthy"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system status");
            return JsonSerializer.Serialize(new 
            { 
                success = false, 
                error = $"Failed to get system status: {ex.Message}",
                timestamp = DateTime.UtcNow,
                apiBaseUrl = _apiBaseUrl,
                status = "unhealthy"
            });
        }
    }
}
