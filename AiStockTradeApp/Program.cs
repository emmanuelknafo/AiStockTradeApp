using AiStockTradeApp.Services.Interfaces;
using AiStockTradeApp.Services.Implementations;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

public class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services
            .AddControllersWithViews()
            .AddViewLocalization()
            .AddDataAnnotationsLocalization();

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