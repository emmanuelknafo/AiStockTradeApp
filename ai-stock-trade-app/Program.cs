using ai_stock_trade_app.Services;
using ai_stock_trade_app.Data;
using Microsoft.EntityFrameworkCore;

public class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllersWithViews();

        // Add health checks
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<StockDataContext>("database");

        // Add Entity Framework
        builder.Services.AddDbContext<StockDataContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                // Fallback for development - using local default instance
                connectionString = "Server=.;Database=StockTraderDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";
            }
            options.UseSqlServer(connectionString);
        });

        // Add HttpClient for external API calls
        builder.Services.AddHttpClient<IStockDataService, StockDataService>();

        // Register custom services
        builder.Services.AddScoped<IStockDataService, StockDataService>();
        builder.Services.AddScoped<IAIAnalysisService, AIAnalysisService>();
        builder.Services.AddScoped<IStockDataRepository, StockDataRepository>();
        builder.Services.AddSingleton<IWatchlistService, WatchlistService>();

        // Add background services
        builder.Services.AddHostedService<CacheCleanupService>();

        // Add session services for user state
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        var app = builder.Build();

        // Ensure database is created and migrated
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<StockDataContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            
            try
            {
                logger.LogInformation("Applying database migrations...");
                context.Database.Migrate();
                logger.LogInformation("Database migrations applied successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying database migrations");
                // In development, create the database if it doesn't exist
                if (app.Environment.IsDevelopment())
                {
                    logger.LogInformation("Creating database for development environment...");
                    context.Database.EnsureCreated();
                }
                else
                {
                    throw;
                }
            }
        }

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        // Health check endpoint
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

        app.Run();
    }
}