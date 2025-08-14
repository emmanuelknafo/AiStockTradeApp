using Microsoft.AspNetCore.Mvc;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Entities.ViewModels;
using AiStockTradeApp.Services.Interfaces;
using System.Text.Json;

namespace AiStockTradeApp.Controllers
{
    public class StockController : Controller
    {
        private readonly IStockDataService _stockDataService;
        private readonly IAIAnalysisService _aiAnalysisService;
        private readonly IWatchlistService _watchlistService;
        private readonly ILogger<StockController> _logger;

        public StockController(
            IStockDataService stockDataService, 
            IAIAnalysisService aiAnalysisService,
            IWatchlistService watchlistService,
            ILogger<StockController> logger)
        {
            _stockDataService = stockDataService;
            _aiAnalysisService = aiAnalysisService;
            _watchlistService = watchlistService;
            _logger = logger;
        }

        public async Task<IActionResult> Dashboard()
        {
            var sessionId = GetSessionId();
            var watchlist = await _watchlistService.GetWatchlistAsync(sessionId);
            
            // Fetch fresh data for all stocks in watchlist
            foreach (var item in watchlist)
            {
                try
                {
                    var stockResponse = await _stockDataService.GetStockQuoteAsync(item.Symbol);
                    if (stockResponse.Success && stockResponse.Data != null)
                    {
                        var (analysis, recommendation, reasoning) = await _aiAnalysisService.GenerateAnalysisAsync(item.Symbol, stockResponse.Data);
                        
                        stockResponse.Data.AIAnalysis = analysis;
                        stockResponse.Data.Recommendation = recommendation;
                        stockResponse.Data.RecommendationReason = reasoning;
                        
                        // Add chart data if charts are enabled
                        var userSettings = GetUserSettings();
                        if (userSettings.ShowCharts)
                        {
                            try
                            {
                                stockResponse.Data.ChartData = await _stockDataService.GetHistoricalDataAsync(item.Symbol);
                            }
                            catch (Exception chartEx)
                            {
                                _logger.LogWarning(chartEx, "Error fetching chart data for {Symbol}", item.Symbol);
                            }
                        }
                        
                        item.StockData = stockResponse.Data;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching data for {Symbol}", item.Symbol);
                }
            }

            var portfolio = await _watchlistService.CalculatePortfolioSummaryAsync(watchlist);
            var alerts = await _watchlistService.GetAlertsAsync(sessionId);

            var viewModel = new DashboardViewModel
            {
                Watchlist = watchlist,
                Portfolio = portfolio,
                Alerts = alerts,
                Settings = GetUserSettings()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AddStock([FromBody] AddStockRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Invalid stock symbol" });
                }

                var sessionId = GetSessionId();
                
                // Validate stock exists by fetching its data
                var stockResponse = await _stockDataService.GetStockQuoteAsync(request.Symbol);
                if (!stockResponse.Success)
                {
                    return Json(new { success = false, message = stockResponse.ErrorMessage ?? "Stock not found" });
                }

                await _watchlistService.AddToWatchlistAsync(sessionId, request.Symbol);
                
                return Json(new { success = true, message = $"Added {request.Symbol.ToUpper()} to watchlist" });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding stock {Symbol}", request.Symbol);
                return Json(new { success = false, message = "Error adding stock to watchlist" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveStock(string symbol)
        {
            try
            {
                var sessionId = GetSessionId();
                await _watchlistService.RemoveFromWatchlistAsync(sessionId, symbol);
                
                return Json(new { success = true, message = $"Removed {symbol.ToUpper()} from watchlist" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing stock {Symbol}", symbol);
                return Json(new { success = false, message = "Error removing stock from watchlist" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ClearWatchlist()
        {
            try
            {
                var sessionId = GetSessionId();
                await _watchlistService.ClearWatchlistAsync(sessionId);
                
                return Json(new { success = true, message = "Watchlist cleared" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing watchlist");
                return Json(new { success = false, message = "Error clearing watchlist" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetStockData(string symbol)
        {
            try
            {
                var stockResponse = await _stockDataService.GetStockQuoteAsync(symbol);
                if (!stockResponse.Success)
                {
                    return Json(new { success = false, message = stockResponse.ErrorMessage });
                }

                var (analysis, recommendation, reasoning) = await _aiAnalysisService.GenerateAnalysisAsync(symbol, stockResponse.Data!);
                
                stockResponse.Data!.AIAnalysis = analysis;
                stockResponse.Data.Recommendation = recommendation;
                stockResponse.Data.RecommendationReason = reasoning;

                return Json(new { success = true, data = stockResponse.Data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching stock data for {Symbol}", symbol);
                return Json(new { success = false, message = "Error fetching stock data" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetChartData(string symbol, int days = 30)
        {
            try
            {
                var chartData = await _stockDataService.GetHistoricalDataAsync(symbol, days);
                return Json(new { success = true, data = chartData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching chart data for {Symbol}", symbol);
                return Json(new { success = false, message = "Error fetching chart data" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSuggestions(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return Json(new List<string>());
                }

                var sessionId = GetSessionId();
                var watchlist = await _watchlistService.GetWatchlistAsync(sessionId);
                var existingSymbols = watchlist.Select(w => w.Symbol.ToUpper()).ToHashSet();

                var suggestions = await _stockDataService.GetStockSuggestionsAsync(query);
                
                // Filter out symbols already in watchlist
                var filteredSuggestions = suggestions.Where(s => !existingSymbols.Contains(s)).ToList();
                
                return Json(filteredSuggestions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting suggestions for query {Query}", query);
                return Json(new List<string>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> SetAlert([FromBody] SetAlertRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Invalid alert request" });
                }

                var sessionId = GetSessionId();
                var watchlist = await _watchlistService.GetWatchlistAsync(sessionId);
                var stockItem = watchlist.FirstOrDefault(w => w.Symbol.Equals(request.Symbol, StringComparison.OrdinalIgnoreCase));
                
                if (stockItem?.StockData == null)
                {
                    return Json(new { success = false, message = "Stock not found in watchlist" });
                }

                var currentPrice = stockItem.StockData.Price;
                var alertType = request.TargetPrice > currentPrice ? "above" : "below";
                
                var alert = new PriceAlert
                {
                    Symbol = request.Symbol.ToUpper(),
                    TargetPrice = request.TargetPrice,
                    AlertType = alertType,
                    CreatedDate = DateTime.UtcNow
                };

                await _watchlistService.AddAlertAsync(sessionId, alert);
                
                return Json(new { 
                    success = true, 
                    message = $"Alert set: {request.Symbol.ToUpper()} {alertType} ${request.TargetPrice}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting alert for {Symbol}", request.Symbol);
                return Json(new { success = false, message = "Error setting price alert" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> RefreshAll()
        {
            try
            {
                var sessionId = GetSessionId();
                var watchlist = await _watchlistService.GetWatchlistAsync(sessionId);
                
                var tasks = watchlist.Select(async item =>
                {
                    try
                    {
                        var stockResponse = await _stockDataService.GetStockQuoteAsync(item.Symbol);
                        if (stockResponse.Success && stockResponse.Data != null)
                        {
                            var (analysis, recommendation, reasoning) = await _aiAnalysisService.GenerateAnalysisAsync(item.Symbol, stockResponse.Data);
                            
                            stockResponse.Data.AIAnalysis = analysis;
                            stockResponse.Data.Recommendation = recommendation;
                            stockResponse.Data.RecommendationReason = reasoning;
                            
                            item.StockData = stockResponse.Data;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error refreshing {Symbol}", item.Symbol);
                    }
                });

                await Task.WhenAll(tasks);
                
                var portfolio = await _watchlistService.CalculatePortfolioSummaryAsync(watchlist);
                
                return Json(new { 
                    success = true, 
                    message = "All stocks refreshed",
                    watchlist = watchlist,
                    portfolio = portfolio
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing all stocks");
                return Json(new { success = false, message = "Error refreshing stocks" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            try
            {
                var sessionId = GetSessionId();
                var exportData = await _watchlistService.GetExportDataAsync(sessionId);
                
                var csv = GenerateCsv(exportData.Watchlist);
                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                
                return File(bytes, "text/csv", $"watchlist_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting CSV");
                return Json(new { success = false, message = "Error exporting CSV" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportJson()
        {
            try
            {
                var sessionId = GetSessionId();
                var exportData = await _watchlistService.GetExportDataAsync(sessionId);
                
                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                
                return File(bytes, "application/json", $"watchlist_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting JSON");
                return Json(new { success = false, message = "Error exporting JSON" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ImportData(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "No file provided" });
                }

                using var reader = new StreamReader(file.OpenReadStream());
                var content = await reader.ReadToEndAsync();
                
                ExportData exportData;
                
                // Determine file type based on extension or content
                var isJsonFile = file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                var isCsvFile = file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
                
                if (isCsvFile || (!isJsonFile && content.StartsWith("Ticker,") || content.Contains("Ticker,Price,Change")))
                {
                    // Parse CSV content
                    exportData = ParseCsvContent(content);
                }
                else
                {
                    // Parse JSON content
                    var deserializedData = JsonSerializer.Deserialize<ExportData>(content, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    if (deserializedData?.Watchlist == null)
                    {
                        return Json(new { success = false, message = "Invalid JSON file format" });
                    }
                    
                    exportData = deserializedData;
                }

                var sessionId = GetSessionId();
                await _watchlistService.ImportDataAsync(sessionId, exportData);
                
                return Json(new { success = true, message = $"Data imported successfully: {exportData.Watchlist.Count} stocks added" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing data");
                return Json(new { success = false, message = "Error importing data: " + ex.Message });
            }
        }

        private string GetSessionId()
        {
            var sessionId = HttpContext.Session.GetString("SessionId");
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("SessionId", sessionId);
            }
            return sessionId;
        }

        private UserSettings GetUserSettings()
        {
            // In a real app, this would come from user preferences storage
            return new UserSettings();
        }

        private static string GenerateCsv(List<WatchlistItem> watchlist)
        {
            var lines = new List<string>
            {
                "Ticker,Price,Change,Percent,Recommendation,Analysis"
            };

            foreach (var item in watchlist.Where(w => w.StockData != null))
            {
                var data = item.StockData!;
                lines.Add($"{data.Symbol},{data.Price},{data.Change},{data.PercentChange},{data.Recommendation ?? "N/A"},{data.AIAnalysis ?? "N/A"}");
            }

            return string.Join("\n", lines);
        }

        private static ExportData ParseCsvContent(string csvContent)
        {
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var watchlist = new List<WatchlistItem>();

            // Skip header line
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = SplitCsvLine(line);
                if (parts.Length >= 6)
                {
                    try
                    {
                        var symbol = parts[0].Trim();
                        var price = decimal.Parse(parts[1].Trim());
                        var change = decimal.Parse(parts[2].Trim());
                        var percent = parts[3].Trim();
                        var recommendation = parts[4].Trim();
                        var analysis = parts[5].Trim();

                        var stockData = new StockData
                        {
                            Symbol = symbol,
                            Price = price,
                            Change = change,
                            PercentChange = percent,
                            Recommendation = recommendation == "N/A" ? null : recommendation,
                            AIAnalysis = analysis == "N/A" ? null : analysis,
                            LastUpdated = DateTime.UtcNow,
                            CompanyName = symbol
                        };

                        watchlist.Add(new WatchlistItem
                        {
                            Symbol = symbol,
                            StockData = stockData,
                            AddedDate = DateTime.UtcNow
                        });
                    }
                    catch
                    {
                        // Skip invalid lines and continue
                        continue;
                    }
                }
            }

            return new ExportData
            {
                Watchlist = watchlist,
                Portfolio = new PortfolioSummary(),
                ExportDate = DateTime.UtcNow,
                Version = "1.0"
            };
        }

        private static string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            var currentField = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            result.Add(currentField.ToString());
            return result.ToArray();
        }
    }
}
