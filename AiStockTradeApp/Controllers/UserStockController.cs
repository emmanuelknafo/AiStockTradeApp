using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.Services.Interfaces;
using Microsoft.Extensions.Localization;
using AiStockTradeApp.Services;

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
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UserStockController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public UserStockController(
            IUserWatchlistService userWatchlistService,
            IStockDataService stockDataService,
            IAIAnalysisService aiAnalysisService,
            UserManager<ApplicationUser> userManager,
            ILogger<UserStockController> logger,
            IStringLocalizer<SharedResource> localizer)
        {
            _userWatchlistService = userWatchlistService;
            _stockDataService = stockDataService;
            _aiAnalysisService = aiAnalysisService;
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
                return user?.Id;
            }
            return null;
        }

        /// <summary>
        /// Enhanced dashboard that automatically loads user's persistent watchlist or session data
        /// </summary>
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var userId = await GetUserIdAsync();
                var sessionId = User.Identity?.IsAuthenticated == true ? null : GetSessionId();

                var watchlist = await _userWatchlistService.GetWatchlistAsync(userId, sessionId);
                var alerts = await _userWatchlistService.GetAlertsAsync(userId, sessionId);

                // Load stock data for each watchlist item
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

                var viewModel = new AiStockTradeApp.Entities.ViewModels.DashboardViewModel
                {
                    Watchlist = watchlist,
                    Alerts = alerts,
                    Portfolio = portfolio,
                    Settings = new AiStockTradeApp.Entities.ViewModels.UserSettings()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                ViewBag.ErrorMessage = _localizer["Error_LoadingDashboard"];
                return View(new AiStockTradeApp.Entities.ViewModels.DashboardViewModel());
            }
        }

        /// <summary>
        /// Add stock to user's persistent watchlist or session
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddStock([FromBody] string symbol)
        {
            try
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    return Json(new { success = false, message = _localizer["Error_InvalidSymbol"] });
                }

                var userId = await GetUserIdAsync();
                var sessionId = User.Identity?.IsAuthenticated == true ? null : GetSessionId();

                // Validate that the stock exists
                var stockQuote = await _stockDataService.GetStockQuoteAsync(symbol);
                if (!stockQuote.Success || stockQuote.Data == null)
                {
                    return Json(new { success = false, message = _localizer["Error_StockNotFound"] });
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
                    message = _localizer["Success_StockAdded", symbol.ToUpper()],
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
                _logger.LogError(ex, "Error adding stock {Symbol}", symbol);
                return Json(new { success = false, message = _localizer["Error_AddingStock"] });
            }
        }

        /// <summary>
        /// Remove stock from user's watchlist
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RemoveStock([FromBody] string symbol)
        {
            try
            {
                var userId = await GetUserIdAsync();
                var sessionId = User.Identity?.IsAuthenticated == true ? null : GetSessionId();

                await _userWatchlistService.RemoveFromWatchlistAsync(symbol, userId, sessionId);

                if (userId != null)
                {
                    _logger.LogInformation("User {UserId} removed {Symbol} from persistent watchlist", userId, symbol);
                }
                else
                {
                    _logger.LogInformation("Session {SessionId} removed {Symbol} from session watchlist", sessionId, symbol);
                }

                return Json(new { success = true, message = _localizer["Success_StockRemoved", symbol.ToUpper()] });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing stock {Symbol}", symbol);
                return Json(new { success = false, message = _localizer["Error_RemovingStock"] });
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

                return Json(new { success = true, message = _localizer["Success_WatchlistCleared"] });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing watchlist");
                return Json(new { success = false, message = _localizer["Error_ClearingWatchlist"] });
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
                return Json(new { success = false, message = _localizer["Error_LoadingWatchlist"] });
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
                    return Json(new { success = false, message = _localizer["Error_InvalidAlert"] });
                }

                var userId = await GetUserIdAsync();
                var sessionId = User.Identity?.IsAuthenticated == true ? null : GetSessionId();

                var watchlist = await _userWatchlistService.GetWatchlistAsync(userId, sessionId);
                var stockItem = watchlist.FirstOrDefault(w => w.Symbol.Equals(request.Symbol, StringComparison.OrdinalIgnoreCase));
                
                if (stockItem?.StockData == null)
                {
                    return Json(new { success = false, message = _localizer["Error_StockNotInWatchlist"] });
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
                return Json(new { success = false, message = _localizer["Error_SettingAlert"] });
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
