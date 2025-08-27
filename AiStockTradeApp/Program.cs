using AiStockTradeApp.Services.Interfaces;
using AiStockTradeApp.Services.Implementations;
using AiStockTradeApp.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using System.Reflection;

namespace AiStockTradeApp
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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

            app.UseSession();
            app.UseAuthorization();

            app.MapStaticAssets();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            await app.RunAsync();
        }
    }
}