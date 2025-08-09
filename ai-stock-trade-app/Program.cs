using ai_stock_trade_app.Services;
using ai_stock_trade_app.Data;
using Microsoft.EntityFrameworkCore;
using Azure.Identity;
using Microsoft.Data.SqlClient;

public class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllersWithViews();

        // Add health checks
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<StockDataContext>("database");

        // Add Entity Framework with Azure AD support
        builder.Services.AddDbContext<StockDataContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                // Fallback for development - using local default instance
                connectionString = "Server=.;Database=StockTraderDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";
            }

            // Configure SQL connection with Azure AD authentication support
            var sqlConnectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            
            // If using Azure AD authentication (detected by Authentication property)
            if (connectionString.Contains("Authentication=Active Directory Default", StringComparison.OrdinalIgnoreCase))
            {
                // For Azure AD authentication, increase timeout and ensure proper configuration
                sqlConnectionStringBuilder.ConnectTimeout = 60; // Increase from default 30 to 60 seconds
                sqlConnectionStringBuilder.CommandTimeout = 60; // Add command timeout
                
                // Configure Entity Framework with longer timeout for Azure AD
                options.UseSqlServer(sqlConnectionStringBuilder.ConnectionString, sqlOptions =>
                {
                    sqlOptions.CommandTimeout(120); // 2 minutes for Entity Framework operations
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });
            }
            else
            {
                // Standard SQL authentication
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                });
            }
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
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            
            // Get connection string and extract server information
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = "Server=.;Database=StockTraderDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";
            }
            
            // Extract server name from connection string
            var serverName = ExtractServerNameFromConnectionString(connectionString);
            var isAzureAd = connectionString.Contains("Authentication=Active Directory Default", StringComparison.OrdinalIgnoreCase);
            
            try
            {
                if (isAzureAd)
                {
                    logger.LogInformation("Applying database migrations with Azure AD authentication to server: {ServerName}...", serverName);
                    logger.LogInformation("This may take longer due to Azure AD token acquisition...");
                }
                else
                {
                    logger.LogInformation("Applying database migrations to server: {ServerName}...", serverName);
                }
                
                // Use a longer timeout for Azure AD connections
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)); // 5 minute timeout
                await context.Database.MigrateAsync(cts.Token);
                
                logger.LogInformation("Database migrations applied successfully to server: {ServerName}", serverName);
            }
            catch (OperationCanceledException)
            {
                logger.LogError("Database migration timed out after 5 minutes for server: {ServerName}", serverName);
                if (isAzureAd)
                {
                    logger.LogError("Azure AD authentication may be taking too long. Check managed identity configuration.");
                }
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error applying database migrations to server: {ServerName}", serverName);
                
                if (isAzureAd && ex.Message.Contains("timeout"))
                {
                    logger.LogError("Azure AD authentication timeout detected. Possible issues:");
                    logger.LogError("1. Managed identity not properly configured");
                    logger.LogError("2. SQL Server firewall blocking connections");
                    logger.LogError("3. Missing SQL user for managed identity");
                }
                
                // In development, create the database if it doesn't exist
                if (app.Environment.IsDevelopment())
                {
                    logger.LogInformation("Creating database for development environment on server: {ServerName}...", serverName);
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

        await app.RunAsync();
    }

    /// <summary>
    /// Extracts the server name from a SQL Server connection string
    /// </summary>
    /// <param name="connectionString">The connection string to parse</param>
    /// <returns>The server name or "Unknown" if not found</returns>
    private static string ExtractServerNameFromConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "Unknown";

        try
        {
            // Parse common server name patterns in SQL Server connection strings
            var lowerConnectionString = connectionString.ToLowerInvariant();
            
            // Look for Server=, Data Source=, or Address= parameters
            var serverPatterns = new[] { "server=", "data source=", "address=" };
            
            foreach (var pattern in serverPatterns)
            {
                var startIndex = lowerConnectionString.IndexOf(pattern);
                if (startIndex >= 0)
                {
                    startIndex += pattern.Length;
                    var endIndex = lowerConnectionString.IndexOf(';', startIndex);
                    if (endIndex == -1)
                        endIndex = connectionString.Length;
                    
                    var serverValue = connectionString.Substring(startIndex, endIndex - startIndex).Trim();
                    
                    // Handle special cases
                    if (serverValue == "." || serverValue == "(local)")
                        return "Local Default Instance";
                    if (serverValue.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase))
                        return $"LocalDB ({serverValue})";
                    if (serverValue.StartsWith(".\\", StringComparison.OrdinalIgnoreCase))
                        return $"Local Named Instance ({serverValue})";
                    
                    return serverValue;
                }
            }
            
            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}