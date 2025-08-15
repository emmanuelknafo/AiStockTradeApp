using System.Net.Http.Json;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AiStockTradeApp.Services.Implementations
{
    // Calls AiStockTradeApp.Api instead of talking to repositories or external providers directly
    public class ApiStockDataServiceClient : IStockDataService
    {
        private readonly HttpClient _http;
        private readonly ILogger<ApiStockDataServiceClient> _logger;
        private readonly string _primaryBaseUrl;
        private readonly string _fallbackBaseUrl;

        public ApiStockDataServiceClient(HttpClient http, IConfiguration config, ILogger<ApiStockDataServiceClient> logger)
        {
            _http = http;
            _logger = logger;

            _primaryBaseUrl = config["StockApi:BaseUrl"] ?? "https://localhost:5001";
            _fallbackBaseUrl = config["StockApi:HttpBaseUrl"] ?? "http://localhost:5256";
            // Do not modify HttpClient.BaseAddress after first request. Use absolute URLs per request instead.
        }

        public async Task<StockQuoteResponse> GetStockQuoteAsync(string symbol)
        {
            try
            {
                var data = await GetWithFallbackAsync<StockData>($"/api/stocks/quote?symbol={Uri.EscapeDataString(symbol)}");
                if (data == null)
                {
                    return new StockQuoteResponse { Success = false, ErrorMessage = "No data returned" };
                }
                return new StockQuoteResponse { Success = true, Data = data };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetStockQuoteAsync for {Symbol}", symbol);
                return new StockQuoteResponse { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<List<string>> GetStockSuggestionsAsync(string query)
        {
            try
            {
                var list = await GetWithFallbackAsync<List<string>>($"/api/stocks/suggestions?query={Uri.EscapeDataString(query)}");
                return list ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Suggestions request failed for {Query}", query);
                return new List<string>();
            }
        }

        public async Task<List<ChartDataPoint>> GetHistoricalDataAsync(string symbol, int days = 30)
        {
            try
            {
                var list = await GetWithFallbackAsync<List<ChartDataPoint>>($"/api/stocks/historical?symbol={Uri.EscapeDataString(symbol)}&days={days}");
                return list ?? new List<ChartDataPoint>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Historical request failed for {Symbol}", symbol);
                return new List<ChartDataPoint>();
            }
        }

        private async Task<T?> GetWithFallbackAsync<T>(string relativeUrl)
        {
            var (value, networkFailure) = await TryGetAsync<T>(_primaryBaseUrl, relativeUrl);
            if (value != null || !networkFailure)
            {
                return value;
            }

            _logger.LogWarning("Primary API endpoint failed at {Base}. Trying fallback {Fallback}", _primaryBaseUrl, _fallbackBaseUrl);
            var (fallbackValue, _) = await TryGetAsync<T>(_fallbackBaseUrl, relativeUrl);
            return fallbackValue;
        }

        private async Task<(T? value, bool networkFailure)> TryGetAsync<T>(string baseUrl, string relativeUrl)
        {
            var url = baseUrl.TrimEnd('/') + relativeUrl;
            try
            {
                using var response = await _http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    // Do not treat non-success status as network failure (prevents unnecessary fallback on 404/400 etc.)
                    return (default, false);
                }
                var data = await response.Content.ReadFromJsonAsync<T>();
                return (data, false);
            }
            catch (HttpRequestException ex)
            {
                // Network-related failure -> allow fallback
                _logger.LogWarning(ex, "HTTP request to {Url} failed", url);
                return (default, true);
            }
        }
    }
}
