using AiStockTradeApp.Services.Interfaces;
using AiStockTradeApp.Services.Implementations;
using AiStockTradeApp.Services;
using AiStockTradeApp.Middleware;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.DataAccess.Data;
using AiStockTradeApp.DataAccess;
using Microsoft.AspNetCore.DataProtection;

namespace AiStockTradeApp
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure comprehensive logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
            
            // Add Application Insights telemetry with robust error handling
            var appInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING") 
                ?? builder.Configuration["ApplicationInsights:ConnectionString"];

            // Validate connection string format before using it
            bool isValidConnectionString = !string.IsNullOrEmpty(appInsightsConnectionString) && 
                                         !appInsightsConnectionString.Contains("#{") && // Check for placeholder tokens
                                         appInsightsConnectionString.Contains("=") && // Basic format check
                                         !appInsightsConnectionString.Equals("invalid-format", StringComparison.OrdinalIgnoreCase);

            if (isValidConnectionString)
            {
                try
                {
                    builder.Services.AddApplicationInsightsTelemetry();
                    Console.WriteLine("Application Insights telemetry configured successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to configure Application Insights: {ex.Message}");
                }
            }
            else
            {
                var displayString = appInsightsConnectionString?.Substring(0, Math.Min(50, appInsightsConnectionString.Length)) ?? "null";
                Console.WriteLine($"Skipping Application Insights configuration - invalid or missing connection string: {displayString}");
            }

            // Add services to the container.
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
            
            // Register custom string localizer that works without resource manifests
            builder.Services.AddSingleton<IStringLocalizer<SharedResource>, SimpleStringLocalizer>();
            
            // Configure localization to use the embedded resources
            builder.Services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedCultures = new[] { "en", "fr" };
                options.SetDefaultCulture(supportedCultures[0])
                    .AddSupportedCultures(supportedCultures)
                    .AddSupportedUICultures(supportedCultures);
            });
            
            builder.Services
                .AddControllersWithViews()
                .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
                .AddDataAnnotationsLocalization(options => {
                    options.DataAnnotationLocalizerProvider = (type, factory) =>
                        factory.Create(typeof(SharedResource));
                });

            // Basic health checks (no DB dependency)
            builder.Services.AddHealthChecks();

            // Configure Entity Framework and Identity
            // Check if we should use in-memory database (for testing)
            var useInMemory = string.Equals(builder.Configuration["USE_INMEMORY_DB"], "true", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(Environment.GetEnvironmentVariable("USE_INMEMORY_DB"), "true", StringComparison.OrdinalIgnoreCase);

            if (useInMemory)
            {
                // Add Identity DbContext with InMemory database for testing
                builder.Services.AddDbContext<ApplicationIdentityContext>(options =>
                    options.UseInMemoryDatabase("TestIdentityDb"));

                // Add StockData DbContext with InMemory database for testing
                builder.Services.AddDbContext<StockDataContext>(options =>
                    options.UseInMemoryDatabase("TestStockDataDb"));
            }
            else
            {
                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

                // Add Identity DbContext with SQL Server for production
                builder.Services.AddDbContext<ApplicationIdentityContext>(options =>
                    options.UseSqlServer(connectionString, sql =>
                    {
                        sql.EnableRetryOnFailure();
                        sql.MigrationsAssembly(typeof(ApplicationIdentityContext).Assembly.FullName);
                    }));

                // Add StockData DbContext with SQL Server for production
                builder.Services.AddDbContext<StockDataContext>(options =>
                    options.UseSqlServer(connectionString, sql =>
                    {
                        sql.EnableRetryOnFailure();
                        sql.MigrationsAssembly(typeof(StockDataContext).Assembly.FullName);
                    }));
            }

            // Configure ASP.NET Core Identity
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Password settings
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 6;
                options.Password.RequiredUniqueChars = 1;

                // Lockout settings
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // User settings
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
                options.User.RequireUniqueEmail = true;

                // SignIn settings
                options.SignIn.RequireConfirmedEmail = false; // Set to true in production with email service
                options.SignIn.RequireConfirmedPhoneNumber = false;
            })
            .AddEntityFrameworkStores<ApplicationIdentityContext>()
            .AddDefaultTokenProviders();

            // Configure Data Protection for container persistence
            // This ensures authentication cookies and tokens remain valid across container restarts
            var dataProtectionKeysPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH") 
                ?? builder.Configuration["DataProtection:KeysPath"] 
                ?? "/app/keys"; // Default container path

            DirectoryInfo? keysDirectory = null;
            bool usePersistedKeys = false;

            try
            {
                keysDirectory = new DirectoryInfo(dataProtectionKeysPath);
                if (!keysDirectory.Exists)
                {
                    keysDirectory.Create();
                    Console.WriteLine($"Created data protection keys directory: {dataProtectionKeysPath}");
                }

                // Test write permissions by creating a temporary file
                var testFile = Path.Combine(dataProtectionKeysPath, $"test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                usePersistedKeys = true;

                Console.WriteLine($"Data protection keys directory verified with write access: {dataProtectionKeysPath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Warning: No write access to data protection keys directory '{dataProtectionKeysPath}': {ex.Message}");
                
                // Try fallback to a temporary directory that should be writable
                try
                {
                    var tempKeysPath = Path.Combine(Path.GetTempPath(), "AiStockTradeApp", "keys");
                    keysDirectory = new DirectoryInfo(tempKeysPath);
                    if (!keysDirectory.Exists)
                    {
                        keysDirectory.Create();
                    }

                    // Test write permissions
                    var testFile = Path.Combine(tempKeysPath, $"test_{Guid.NewGuid():N}.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    usePersistedKeys = true;

                    Console.WriteLine($"Using fallback data protection keys directory: {tempKeysPath}");
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"Warning: Fallback keys directory also failed: {fallbackEx.Message}");
                    Console.WriteLine("Will use ephemeral data protection keys (authentication will reset on container restart)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to configure persistent data protection keys: {ex.Message}");
                Console.WriteLine("Will use ephemeral data protection keys (authentication will reset on container restart)");
            }

            // Configure data protection based on what we were able to set up
            var dataProtectionBuilder = builder.Services.AddDataProtection()
                .SetApplicationName("AiStockTradeApp") // Must be same across all instances
                .SetDefaultKeyLifetime(TimeSpan.FromDays(90)); // Keys valid for 90 days

            if (usePersistedKeys && keysDirectory != null)
            {
                dataProtectionBuilder.PersistKeysToFileSystem(keysDirectory);
                Console.WriteLine($"Data protection configured with persistent keys at: {keysDirectory.FullName}");
            }
            else
            {
                Console.WriteLine("Data protection configured with ephemeral keys (will reset on restart)");
            }

            // Configure Application Cookie settings
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

            // Register HttpClient for UI -> API client
            builder.Services.AddHttpClient<ApiStockDataServiceClient>((sp, http) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var baseUrl = config["StockApi:BaseUrl"] ?? "https://localhost:5001";
                http.BaseAddress = new Uri(baseUrl);
            });

            // Redirect IStockDataService to API client implementation
            builder.Services.AddScoped<IStockDataService, ApiStockDataServiceClient>();

            // Register other services used by UI
            builder.Services.AddScoped<IAIAnalysisService, AIAnalysisService>();
            
            // Register the new user-aware watchlist service that automatically saves for logged-in users
            builder.Services.AddScoped<IUserWatchlistService, UserWatchlistService>();
            
            // Keep the old session-based service for backward compatibility
            builder.Services.AddSingleton<IWatchlistService, WatchlistService>();
            
            builder.Services.AddScoped<IAuthenticationDiagnosticsService, AuthenticationDiagnosticsService>();

            // Note: Removed DbContext/Repository/CacheCleanupService from UI. API owns data/caching.

            // Add session services for user state
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            var app = builder.Build();

            // Ensure database exists and apply migrations for Identity context in local mode
            if (!useInMemory)
            {
                using var scope = app.Services.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbMigration");
                
                try
                {
                    // Apply Identity migrations
                    var identityContext = scope.ServiceProvider.GetRequiredService<ApplicationIdentityContext>();
                    await identityContext.Database.MigrateAsync();
                    logger.LogInformation("Identity database migrations applied successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to apply Identity database migrations.");
                }
            }

            // Add comprehensive request logging for monitoring
            app.UseRequestLogging();

            // Localization: supported cultures
            var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("fr") };
            var localizationOptions = new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture("en"),
                SupportedCultures = supportedCultures,
                SupportedUICultures = supportedCultures
            };
            
            // Add providers in order of preference
            localizationOptions.RequestCultureProviders.Clear();
            localizationOptions.RequestCultureProviders.Add(new CookieRequestCultureProvider());
            localizationOptions.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
            
            app.UseRequestLocalization(localizationOptions);

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.MapHealthChecks("/health");

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseSession();

            // Add watchlist migration middleware after authentication but before routing
            app.UseWatchlistMigration();

            app.MapStaticAssets();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            await app.RunAsync();
        }
    }
}