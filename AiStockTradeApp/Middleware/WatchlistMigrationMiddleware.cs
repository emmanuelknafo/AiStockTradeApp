using AiStockTradeApp.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using AiStockTradeApp.Entities.Models;

namespace AiStockTradeApp.Middleware
{
    /// <summary>
    /// Middleware that automatically migrates session-based watchlist data to user accounts upon login
    /// </summary>
    public class WatchlistMigrationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<WatchlistMigrationMiddleware> _logger;

        public WatchlistMigrationMiddleware(
            RequestDelegate next,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<WatchlistMigrationMiddleware> logger)
        {
            _next = next;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Check if this is a successful login POST request
            if (context.Request.Method == "POST" && 
                context.Request.Path.StartsWithSegments("/Account/Login", StringComparison.OrdinalIgnoreCase))
            {
                // Store the session ID before processing the login
                var sessionId = context.Session.GetString("SessionId");
                
                // Continue with the request (process login)
                await _next(context);
                
                // After successful login, check if user is authenticated and migrate data
                if (context.User?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(sessionId))
                {
                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                        var userWatchlistService = scope.ServiceProvider.GetRequiredService<IUserWatchlistService>();

                        var user = await userManager.GetUserAsync(context.User);
                        if (user != null)
                        {
                            // Check if there's any session data to migrate
                            var sessionWatchlist = await userWatchlistService.GetWatchlistAsync(null, sessionId);
                            var sessionAlerts = await userWatchlistService.GetAlertsAsync(null, sessionId);

                            if (sessionWatchlist.Any() || sessionAlerts.Any())
                            {
                                await userWatchlistService.MigrateSessionToUserAsync(sessionId, user.Id);
                                
                                _logger.LogInformation("Successfully migrated session {SessionId} data to user {UserId}: {WatchlistCount} watchlist items, {AlertCount} alerts",
                                    sessionId, user.Id, sessionWatchlist.Count, sessionAlerts.Count);

                                // Set a flag in session to show migration success message
                                context.Session.SetString("WatchlistMigrated", "true");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error migrating session {SessionId} data after login", sessionId);
                        // Don't let migration errors affect the login process
                    }
                }
            }
            else
            {
                await _next(context);
            }
        }
    }

    /// <summary>
    /// Extension method to register the watchlist migration middleware
    /// </summary>
    public static class WatchlistMigrationMiddlewareExtensions
    {
        public static IApplicationBuilder UseWatchlistMigration(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<WatchlistMigrationMiddleware>();
        }
    }
}
