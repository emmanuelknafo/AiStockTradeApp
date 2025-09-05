using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.Entities.ViewModels;
using AiStockTradeApp.Services.Interfaces;
using Microsoft.Extensions.Localization;
using AiStockTradeApp.Services;
using System.Text.Json;

namespace AiStockTradeApp.Controllers
{
    /// <summary>
    /// Enhanced stock controller that integrates user authentication with watchlist management
    /// Automatically saves watchlists for logged-in users and migrates session data on login
    /// </summary>
    public class UserStockController : Controller
    {
        private readonly IUserWatchlistService _userWatchlistService;
        private readonly IStockDataService _stockDataService;
    private readonly IAIAnalysisService _aiAnalysisService;
    private readonly IWatchlistQuoteAggregator _quoteAggregator;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UserStockController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public UserStockController(
            IUserWatchlistService userWatchlistService,
            IStockDataService stockDataService,
            IAIAnalysisService aiAnalysisService,
            IWatchlistQuoteAggregator quoteAggregator,
            UserManager<ApplicationUser> userManager,
            ILogger<UserStockController> logger,
            IStringLocalizer<SharedResource> localizer)
        {
            _userWatchlistService = userWatchlistService;
            _stockDataService = stockDataService;
            _aiAnalysisService = aiAnalysisService;
            _quoteAggregator = quoteAggregator;
            _userManager = userManager;
            _logger = logger;
            _localizer = localizer;
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

        private async Task<string?> GetUserIdAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    return user.Id;
                }
                else
                {
                    // User is authenticated but doesn't exist in database - this can happen
                    // if the database was reset but auth cookies remain. Log this issue.
                    _logger.LogWarning("Authenticated user not found in database. Authentication cookie may be stale. User: {UserName}", 
                        User.Identity.Name);
                }
            }
            return null;
        }

        /// <summary>
        /// Get user and session IDs with fallback logic for stale authentication
        /// </summary>
        private async Task<(string? userId, string? sessionId)> GetUserAndSessionAsync()
        {
            var userId = await GetUserIdAsync();
            // If user is authenticated but doesn't exist in DB, fall back to session storage
            var sessionId = (userId == null) ? GetSessionId() : null;
            return (userId, sessionId);
        }

        /// <summary>
        /// Enhanced dashboard that automatically loads user's persistent watchlist or session data
        /// </summary>
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var userId = await GetUserIdAsync();
                // Always fall back to session storage when userId could not be resolved
                var sessionId = userId == null ? GetSessionId() : null;
                using var op = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["UserId"] = userId,
                    ["SessionId"] = sessionId,
                    ["CorrelationId"] = HttpContext.TraceIdentifier
                });

                _logger.LogInformation("Loading dashboard for {ContextType} {ContextId}", userId != null ? "User" : "Session", (object?)userId ?? sessionId!);

                var watchlist = await _userWatchlistService.GetWatchlistAsync(userId, sessionId);
                var alerts = await _userWatchlistService.GetAlertsAsync(userId, sessionId);

                List<string> errors;
                try
                {
                    errors = (await _quoteAggregator.PopulateQuotesAsync(watchlist)).ToList();
                }
                catch (Exception exAgg)
                {
                    _logger.LogError(exAgg, "Quote aggregation failed");
                    errors = new List<string> { _localizer["Error_PartialData"].Value ?? "Some symbols failed to load" };
                }

                var portfolio = await _userWatchlistService.CalculatePortfolioSummaryAsync(watchlist);

                var distinctErrors = errors.Distinct().Take(3).ToList();
                var viewModel = new AiStockTradeApp.Entities.ViewModels.DashboardViewModel
                {
                    Watchlist = watchlist,
                    Alerts = alerts,
                    Portfolio = portfolio,
                    Settings = new AiStockTradeApp.Entities.ViewModels.UserSettings(),
                    ErrorMessage = distinctErrors.Any() ? string.Join("; ", distinctErrors) : null
                };

                if (viewModel.ErrorMessage != null)
                {
                    _logger.LogWarning("Dashboard loaded with partial errors: {ErrorMessage}", viewModel.ErrorMessage);
                }

                _logger.LogInformation("Dashboard loaded: WatchlistCount={Count}, Alerts={Alerts}, TotalValue={Value}", watchlist.Count, alerts.Count, portfolio.TotalValue);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                var errorText = _localizer["Error_LoadingDashboard"].Value ?? "Error_LoadingDashboard";
                var failedModel = new AiStockTradeApp.Entities.ViewModels.DashboardViewModel
                {
                    ErrorMessage = errorText
                };
                return View(failedModel);
            }
        }

        /// <summary>
        /// Add stock to user's persistent watchlist or session
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddStock([FromBody] AddStockRequest request)
        {
            try
            {
                if (!ModelState.IsValid || request?.Symbol == null)
                {
                    return Json(new { success = false, message = _localizer["Error_InvalidSymbol"].Value });
                }

                var symbol = request.Symbol.ToUpper().Trim();
                var (userId, sessionId) = await GetUserAndSessionAsync();
                
                _logger.LogInformation("AddStock called for {Symbol}. UserId: {UserId}, SessionId: {SessionId}", 
                    symbol, userId ?? "[null]", sessionId ?? "[null]");

                // Validate that the stock exists
                var stockQuote = await _stockDataService.GetStockQuoteAsync(symbol);
                if (!stockQuote.Success || stockQuote.Data == null)
                {
                    _logger.LogWarning("Failed to get stock quote for {Symbol}. Success: {Success}, ErrorMessage: {ErrorMessage}", 
                        symbol, stockQuote.Success, stockQuote.ErrorMessage);
                    return Json(new { success = false, message = stockQuote.ErrorMessage ?? _localizer["Error_StockNotFound"].Value });
                }

                await _userWatchlistService.AddToWatchlistAsync(symbol, userId, sessionId);

                // Generate AI analysis for the stock
                string? analysis = null;
                try
                {
                    var (analysisText, recommendation, reasoning) = await _aiAnalysisService.GenerateAnalysisAsync(symbol, stockQuote.Data);
                    analysis = analysisText;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate AI analysis for {Symbol}", symbol);
                }

                if (userId != null)
                {
                    _logger.LogInformation("User {UserId} added {Symbol} to persistent watchlist", userId, symbol);
                }
                else
                {
                    _logger.LogInformation("Session {SessionId} added {Symbol} to session watchlist", sessionId, symbol);
                }

                return Json(new
                {
                    success = true,
                    message = _localizer["Success_StockAdded", symbol.ToUpper()].Value,
                    stockData = new
                    {
                        symbol = stockQuote.Data.Symbol,
                        price = stockQuote.Data.Price,
                        change = stockQuote.Data.Change,
                        changePercent = stockQuote.Data.PercentChange,
                        volume = 0, // Volume is in ChartDataPoint, not StockData
                        lastUpdated = stockQuote.Data.LastUpdated,
                        analysis = analysis
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding stock {Symbol}", request?.Symbol);
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Remove stock from user's watchlist
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RemoveStock(string symbol)
        {
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    return Json(new { success = false, message = _localizer["Error_InvalidSymbol"].Value });
                }

                var (userId, sessionId) = await GetUserAndSessionAsync();

                await _userWatchlistService.RemoveFromWatchlistAsync(symbol, userId, sessionId);

                if (userId != null)
                {
                    _logger.LogInformation("User {UserId} removed {Symbol} from persistent watchlist", userId, symbol);
                }
                else
                {
                    _logger.LogInformation("Session {SessionId} removed {Symbol} from session watchlist", sessionId, symbol);
                }

                return Json(new { success = true, message = _localizer["Success_StockRemoved", symbol.ToUpper()].Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing stock {Symbol}", symbol);
                return Json(new { success = false, message = _localizer["Error_RemovingStock"].Value });
            }
        }

        /// <summary>
        /// Clear user's entire watchlist
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ClearWatchlist()
        {
            try
            {
                var userId = await GetUserIdAsync();
                var sessionId = User.Identity?.IsAuthenticated == true ? null : GetSessionId();

                await _userWatchlistService.ClearWatchlistAsync(userId, sessionId);

                if (userId != null)
                {
                    _logger.LogInformation("User {UserId} cleared persistent watchlist", userId);
                }
                else
                {
                    _logger.LogInformation("Session {SessionId} cleared session watchlist", sessionId);
                }

                return Json(new { success = true, message = _localizer["Success_WatchlistCleared"].Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing watchlist");
                return Json(new { success = false, message = _localizer["Error_ClearingWatchlist"].Value });
            }
        }

        /// <summary>
        /// Get updated stock data for watchlist
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetWatchlist()
        {
            try
            {
                var userId = await GetUserIdAsync();
                var sessionId = User.Identity?.IsAuthenticated == true ? null : GetSessionId();

                var watchlist = await _userWatchlistService.GetWatchlistAsync(userId, sessionId);

                // Load current stock data
                foreach (var item in watchlist)
                {
                    try
                    {
                        var stockQuote = await _stockDataService.GetStockQuoteAsync(item.Symbol);
                        item.StockData = stockQuote.Data;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load stock data for {Symbol}", item.Symbol);
                    }
                }

                var portfolio = await _userWatchlistService.CalculatePortfolioSummaryAsync(watchlist);

                return Json(new
                {
                    success = true,
                    watchlist = watchlist.Select(w => new
                    {
                        symbol = w.Symbol,
                        addedDate = w.AddedDate,
                        stockData = w.StockData != null ? new
                        {
                            symbol = w.StockData.Symbol,
                            price = w.StockData.Price,
                            change = w.StockData.Change,
                            changePercent = w.StockData.PercentChange,
                            volume = 0, // Volume is in ChartDataPoint, not StockData
                            lastUpdated = w.StockData.LastUpdated
                        } : null
                    }),
                    portfolio = new
                    {
                        totalValue = portfolio.TotalValue,
                        totalChange = portfolio.TotalChange,
                        totalChangePercent = portfolio.TotalChangePercent,
                        stockCount = portfolio.StockCount,
                        lastUpdated = portfolio.LastUpdated,
                        changeClass = portfolio.ChangeClass,
                        changePrefix = portfolio.ChangePrefix
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting watchlist");
                return Json(new { success = false, message = _localizer["Error_LoadingWatchlist"].Value });
            }
        }

        /// <summary>
        /// Migrate session data to user account after login
        /// This should be called automatically after successful login
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> MigrateSessionData()
        {
            try
            {
                var userId = await GetUserIdAsync();
                if (userId == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var sessionId = GetSessionId();
                await _userWatchlistService.MigrateSessionToUserAsync(sessionId, userId);

                _logger.LogInformation("Migrated session {SessionId} data to user {UserId}", sessionId, userId);

                return Json(new { success = true, message = "Session data migrated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating session data");
                return Json(new { success = false, message = "Error migrating session data" });
            }
        }

        /// <summary>
        /// Set price alert for authenticated users
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SetAlert([FromBody] AiStockTradeApp.Entities.SetAlertRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = _localizer["Error_InvalidAlert"].Value });
                }

                var userId = await GetUserIdAsync();
                var sessionId = User.Identity?.IsAuthenticated == true ? null : GetSessionId();

                var watchlist = await _userWatchlistService.GetWatchlistAsync(userId, sessionId);
                var stockItem = watchlist.FirstOrDefault(w => w.Symbol.Equals(request.Symbol, StringComparison.OrdinalIgnoreCase));
                
                if (stockItem?.StockData == null)
                {
                    return Json(new { success = false, message = _localizer["Error_StockNotInWatchlist"].Value });
                }

                var currentPrice = stockItem.StockData.Price;
                var alertType = request.TargetPrice > currentPrice ? "above" : "below";
                
                var alert = new AiStockTradeApp.Entities.PriceAlert
                {
                    Symbol = request.Symbol.ToUpper(),
                    TargetPrice = request.TargetPrice,
                    AlertType = alertType,
                    CreatedDate = DateTime.UtcNow
                };

                await _userWatchlistService.AddAlertAsync(alert, userId, sessionId);
                
                return Json(new { 
                    success = true, 
                    message = _localizer["Success_AlertSet", request.Symbol.ToUpper(), alertType, request.TargetPrice]
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting alert for {Symbol}", request.Symbol);
                return Json(new { success = false, message = _localizer["Error_SettingAlert"].Value });
            }
        }

        /// <summary>
        /// Get user's watchlist management page with advanced features for authenticated users
        /// </summary>
        [Authorize]
        public async Task<IActionResult> ManageWatchlist()
        {
            try
            {
                var userId = await GetUserIdAsync();
                if (userId == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var userWatchlistItems = await _userWatchlistService.GetUserWatchlistItemsAsync(userId);
                var userAlerts = await _userWatchlistService.GetUserAlertsAsync(userId);

                // Load stock data for each item
                foreach (var item in userWatchlistItems)
                {
                    try
                    {
                        var stockQuote = await _stockDataService.GetStockQuoteAsync(item.Symbol);
                        // You could add a property to store this temporarily or use ViewBag
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load stock data for {Symbol}", item.Symbol);
                    }
                }

                ViewBag.UserWatchlistItems = userWatchlistItems;
                ViewBag.UserAlerts = userAlerts;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading watchlist management page for user");
                ViewBag.ErrorMessage = _localizer["Error_LoadingWatchlistManagement"];
                return View();
            }
        }

        /// <summary>
        /// Update watchlist item alias (authenticated users only)
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> SetItemAlias([FromBody] SetItemAliasRequest request)
        {
            try
            {
                var userId = await GetUserIdAsync();
                if (userId == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                await _userWatchlistService.SetWatchlistItemAliasAsync(request.ItemId, request.Alias);

                return Json(new { success = true, message = "Alias updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting alias for watchlist item {ItemId}", request.ItemId);
                return Json(new { success = false, message = "Error updating alias" });
            }
        }

        /// <summary>
        /// Update watchlist item target price (authenticated users only)
        /// </summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> SetItemTargetPrice([FromBody] SetItemTargetPriceRequest request)
        {
            try
            {
                var userId = await GetUserIdAsync();
                if (userId == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                await _userWatchlistService.SetWatchlistItemTargetPriceAsync(request.ItemId, request.TargetPrice);

                return Json(new { success = true, message = "Target price updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting target price for watchlist item {ItemId}", request.ItemId);
                return Json(new { success = false, message = "Error updating target price" });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UpdateAlias([FromBody] UpdateAliasRequest request)
        {
            try
            {
                var userId = await GetUserIdAsync();
                if (userId == null)
                {
                    return Json(new { success = false, message = "Not authenticated" });
                }

                await _userWatchlistService.UpdateWatchlistItemAliasAsync(request.ItemId, userId, request.Alias);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating alias for item {ItemId}", request.ItemId);
                return Json(new { success = false, message = "Failed to update alias" });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UpdateTargetPrice([FromBody] UpdateTargetPriceRequest request)
        {
            try
            {
                var userId = await GetUserIdAsync();
                if (userId == null)
                {
                    return Json(new { success = false, message = "Not authenticated" });
                }

                await _userWatchlistService.UpdateWatchlistItemTargetPriceAsync(request.ItemId, userId, request.TargetPrice);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating target price for item {ItemId}", request.ItemId);
                return Json(new { success = false, message = "Failed to update target price" });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleAlerts([FromBody] ToggleAlertsRequest request)
        {
            try
            {
                var userId = await GetUserIdAsync();
                if (userId == null)
                {
                    return Json(new { success = false, message = "Not authenticated" });
                }

                await _userWatchlistService.UpdateWatchlistItemAlertsAsync(request.ItemId, userId, request.Enabled);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling alerts for item {ItemId}", request.ItemId);
                return Json(new { success = false, message = "Failed to toggle alerts" });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RemoveFromWatchlist([FromBody] RemoveFromWatchlistRequest request)
        {
            try
            {
                var userId = await GetUserIdAsync();
                if (userId == null)
                {
                    return Json(new { success = false, message = "Not authenticated" });
                }

                await _userWatchlistService.RemoveFromWatchlistByIdAsync(request.ItemId, userId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item {ItemId} from watchlist", request.ItemId);
                return Json(new { success = false, message = "Failed to remove item" });
            }
        }

        /// <summary>
        /// Get chart data for a specific stock symbol
        /// </summary>
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
                return Json(new { success = false, message = _localizer["Error_FetchingChartData"].Value });
            }
        }

        /// <summary>
        /// Get stock symbol suggestions for search
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSuggestions(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return Json(new List<string>());
                }

                var userId = await GetUserIdAsync();
                var sessionId = User.Identity?.IsAuthenticated == true ? null : GetSessionId();
                var watchlist = await _userWatchlistService.GetWatchlistAsync(userId, sessionId);
                var existingSymbols = watchlist.Select(w => w.Symbol.ToUpper()).ToHashSet();

                var suggestions = await _stockDataService.GetStockSuggestionsAsync(query);
                
                // Filter out symbols already in watchlist
                var filteredSuggestions = suggestions.Where(s => !existingSymbols.Contains(s)).ToList();
                
                return Json(filteredSuggestions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting suggestions for query: {Query}", query);
                return Json(new List<string>());
            }
        }

        /// <summary>
        /// Export watchlist data as CSV
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            try
            {
                var userId = await GetUserIdAsync();
                var sessionId = User.Identity?.IsAuthenticated == true ? null : GetSessionId();
                var watchlist = await _userWatchlistService.GetWatchlistAsync(userId, sessionId);
                
                // Load current stock data for export
                foreach (var item in watchlist)
                {
                    try
                    {
                        var stockQuote = await _stockDataService.GetStockQuoteAsync(item.Symbol);
                        item.StockData = stockQuote.Data;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load stock data for export: {Symbol}", item.Symbol);
                    }
                }
                
                var csv = GenerateCsv(watchlist);
                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                
                return File(bytes, "text/csv", $"watchlist_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting CSV");
                return Json(new { success = false, message = _localizer["Error_ExportingCsv"].Value });
            }
        }

        /// <summary>
        /// Export watchlist data as JSON
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExportJson()
        {
            try
            {
                var userId = await GetUserIdAsync();
                var sessionId = User.Identity?.IsAuthenticated == true ? null : GetSessionId();
                var watchlist = await _userWatchlistService.GetWatchlistAsync(userId, sessionId);
                var alerts = await _userWatchlistService.GetAlertsAsync(userId, sessionId);
                var portfolio = await _userWatchlistService.CalculatePortfolioSummaryAsync(watchlist);
                
                // Load current stock data for export
                foreach (var item in watchlist)
                {
                    try
                    {
                        var stockQuote = await _stockDataService.GetStockQuoteAsync(item.Symbol);
                        item.StockData = stockQuote.Data;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load stock data for export: {Symbol}", item.Symbol);
                    }
                }

                var exportData = new ExportData
                {
                    Watchlist = watchlist,
                    Portfolio = portfolio,
                    ExportDate = DateTime.UtcNow,
                    Version = "1.0"
                };
                
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
                return Json(new { success = false, message = _localizer["Error_ExportingJson"] });
            }
        }

        /// <summary>
        /// Import watchlist data from CSV or JSON file
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ImportData(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = _localizer["Error_NoFileProvided"] });
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
                        return Json(new { success = false, message = _localizer["Error_InvalidJsonFormat"] });
                    }
                    
                    exportData = deserializedData;
                }

                var userId = await GetUserIdAsync();
                var sessionId = User.Identity?.IsAuthenticated == true ? null : GetSessionId();

                // Import the data using our service
                foreach (var item in exportData.Watchlist)
                {
                    try
                    {
                        await _userWatchlistService.AddToWatchlistAsync(item.Symbol, userId, sessionId);
                    }
                    catch (InvalidOperationException)
                    {
                        // Skip if already exists
                        continue;
                    }
                }
                
                return Json(new { success = true, message = _localizer["Success_DataImported", exportData.Watchlist.Count] });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing data");
                return Json(new { success = false, message = _localizer["Error_ImportingData"] + ": " + ex.Message });
            }
        }

        /// <summary>
        /// Generate CSV content from watchlist
        /// </summary>
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

        /// <summary>
        /// Parse CSV content into ExportData
        /// </summary>
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

        /// <summary>
        /// Split CSV line respecting quoted fields
        /// </summary>
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

    // Request models for watchlist management
    public class UpdateAliasRequest
    {
        public int ItemId { get; set; }
        public string? Alias { get; set; }
    }

    public class UpdateTargetPriceRequest
    {
        public int ItemId { get; set; }
        public decimal? TargetPrice { get; set; }
    }

    public class ToggleAlertsRequest
    {
        public int ItemId { get; set; }
        public bool Enabled { get; set; }
    }

    public class RemoveFromWatchlistRequest
    {
        public int ItemId { get; set; }
    }

    // Request models for watchlist management
    public class SetItemAliasRequest
    {
        public int ItemId { get; set; }
        public string? Alias { get; set; }
    }

    public class SetItemTargetPriceRequest
    {
        public int ItemId { get; set; }
        public decimal? TargetPrice { get; set; }
    }
}
