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
            
            // Add Application Insights telemetry
            if (!string.IsNullOrEmpty(builder.Configuration["ApplicationInsights:ConnectionString"]))
            {
                builder.Services.AddApplicationInsightsTelemetry(options =>
                {
                    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
                });
                builder.Logging.AddApplicationInsights(
                    configureTelemetryConfiguration: (config) => 
                        config.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"],
                    configureApplicationInsightsLoggerOptions: (options) => { }
                );
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
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // Add Identity DbContext
            builder.Services.AddDbContext<ApplicationIdentityContext>(options =>
                options.UseSqlServer(connectionString));

            // Add StockData DbContext (for user-specific data like watchlists)
            builder.Services.AddDbContext<StockDataContext>(options =>
                options.UseSqlServer(connectionString));

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

            app.MapStaticAssets();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            await app.RunAsync();
        }
    }
}