using ai_stock_trade_app.Services;
using ai_stock_trade_app.Data;
using Microsoft.EntityFrameworkCore;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

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

        // Add Entity Framework with optional in-memory provider for UI tests
        var useInMemory = string.Equals(Environment.GetEnvironmentVariable("USE_INMEMORY_DB"), "true", StringComparison.OrdinalIgnoreCase);
        var isAzureAppService = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
        var isProdLike = builder.Environment.IsProduction() || (isAzureAppService && !builder.Environment.IsDevelopment());

        // Enforce external SQL when hosted in Azure App Service or Production-like environments
        if (isProdLike && useInMemory)
        {
            // Do not allow in-memory DB when hosted; force external SQL
            useInMemory = false;
        }

        if (useInMemory)
        {
            builder.Services.AddDbContext<StockDataContext>(options =>
                options.UseInMemoryDatabase("UiTestDb"));
        }
        else
        {
            builder.Services.AddDbContext<StockDataContext>(options =>
            {
                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    if (isProdLike)
                    {
                        // Fail fast in hosted/prod if no connection string is configured
                        throw new InvalidOperationException("DefaultConnection is not configured. In hosted/production environments, an external Azure SQL connection string is required.");
                    }
                    else
                    {
                        // Fallback for local development only
                        connectionString = "Server=.;Database=StockTraderDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";
                    }
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
        }

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

    // Ensure database is created and migrated with resilient Azure AD handling
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<StockDataContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var useInMemoryRuntime = string.Equals(Environment.GetEnvironmentVariable("USE_INMEMORY_DB"), "true", StringComparison.OrdinalIgnoreCase);
            var isAzureHostedRuntime = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

            // Database mode summary logging
            logger.LogInformation("[DB] Environment: {Env} | AzureHosted: {AzureHosted} | USE_INMEMORY_DB={UseInMemory}", app.Environment.EnvironmentName, isAzureHostedRuntime, useInMemoryRuntime);
            if (isAzureHostedRuntime && useInMemoryRuntime)
            {
                logger.LogWarning("[DB] USE_INMEMORY_DB=true detected on Azure hosted environment – in-memory will be ignored; external SQL is required.");
                useInMemoryRuntime = false;
            }
            if (useInMemoryRuntime)
            {
                logger.LogInformation("[MIGRATIONS] Skipping migrations – using in-memory provider (USE_INMEMORY_DB=true)");
            }
            
            // Get connection string and extract server information
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                if (app.Environment.IsDevelopment() && !isAzureHostedRuntime)
                {
                    connectionString = "Server=.;Database=StockTraderDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";
                    logger.LogWarning("[DB] DefaultConnection not set; using local development fallback connection string.");
                }
                else
                {
                    logger.LogCritical("[DB] DefaultConnection is missing in a hosted/production environment. Configure Azure SQL connection in application settings.");
                    throw new InvalidOperationException("DefaultConnection is not configured in hosted/production environment.");
                }
            }
            
            // Extract server name from connection string
            var serverName = ExtractServerNameFromConnectionString(connectionString);
            string databaseName = "Unknown";
            try
            {
                var csb = new SqlConnectionStringBuilder(connectionString);
                databaseName = csb.InitialCatalog;
            }
            catch { /* ignore */ }
            var isAzureAd = connectionString.Contains("Authentication=Active Directory Default", StringComparison.OrdinalIgnoreCase);

            // Diagnostic logging about authentication mode & potential misconfiguration
            try
            {
                if (isAzureAd)
                {
                    logger.LogInformation("[DB] Mode: External SQL (AAD). Server={Server} Database={Database}", serverName, databaseName);
                    logger.LogInformation("[MIGRATIONS] Detected Azure AD authentication mode in DefaultConnection (Authentication=Active Directory Default)");
                }
                else
                {
                    logger.LogInformation("[DB] Mode: External SQL (SQL Auth). Server={Server} Database={Database}", serverName, databaseName);
                    var lowered = connectionString.ToLowerInvariant();
                    var hasSqlAdmin = lowered.Contains("user id=sqladmin") || lowered.Contains("uid=sqladmin");
                    var passwordFragment = System.Text.RegularExpressions.Regex.Match(connectionString, @"Password=([^;]*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var pwdValue = passwordFragment.Success ? passwordFragment.Groups[1].Value : "(absent)";
                    var blankPwd = passwordFragment.Success && string.IsNullOrWhiteSpace(pwdValue);
                    logger.LogInformation("[MIGRATIONS] Using SQL authentication (no AAD token). UserIdPresent={UserPresent} BlankPassword={BlankPwd}", hasSqlAdmin, blankPwd);
                    if (hasSqlAdmin && blankPwd)
                    {
                        logger.LogWarning("[MIGRATIONS] Detected 'sqladmin' with blank password in connection string. Ensure pipeline passes sqlAdminPassword to Bicep or enable AAD-only auth.");
                    }
                }
            }
            catch (Exception diagEx)
            {
                logger.LogDebug(diagEx, "[MIGRATIONS] Connection string diagnostics failed (non-fatal)");
            }
            
            var maxAttempts = isAzureAd ? 6 : 1; // up to ~ (0+5+10+20+30) seconds + work
            var delays = new[] { 0, 5, 10, 20, 30, 45 }; // seconds
            Exception? lastException = null;
            for (var attempt = 1; attempt <= maxAttempts && !useInMemoryRuntime; attempt++)
            {
                var delay = isAzureAd ? delays[Math.Min(attempt - 1, delays.Length - 1)] : 0;
                if (delay > 0)
                {
                    logger.LogInformation("[MIGRATIONS] Waiting {Delay}s before attempt {Attempt}/{MaxAttempts} (Azure AD)", delay, attempt, maxAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(delay));
                }

                try
                {
                    if (isAzureAd)
                    {
                        logger.LogInformation("[MIGRATIONS] Attempt {Attempt}/{MaxAttempts} applying migrations with Azure AD auth to {ServerName}", attempt, maxAttempts, serverName);
                    }
                    else if (attempt == 1)
                    {
                        logger.LogInformation("[MIGRATIONS] Applying migrations to {ServerName}", serverName);
                    }

                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                    await context.Database.MigrateAsync(cts.Token);
                    logger.LogInformation("[MIGRATIONS] Success on attempt {Attempt}", attempt);
                    lastException = null;
                    break; // success
                }
                catch (OperationCanceledException oce)
                {
                    lastException = oce;
                    logger.LogWarning(oce, "[MIGRATIONS] Timeout on attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);
                }
                catch (SqlException sqlEx) when (isAzureAd)
                {
                    lastException = sqlEx;
                    // Common AAD transient issues: 18456 login failed, network, principal not found yet
                    var msg = sqlEx.Message;
                    if (sqlEx.Number == 18456 || msg.Contains("Login failed", StringComparison.OrdinalIgnoreCase) || msg.Contains("transient", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning(sqlEx, "[MIGRATIONS] Transient Azure AD login/mapping issue on attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);
                        continue;
                    }
                    logger.LogError(sqlEx, "[MIGRATIONS] Non-transient SqlException on attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (isAzureAd && ex.Message.Contains("Login failed", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning(ex, "[MIGRATIONS] Generic login failed (Azure AD) attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);
                        continue;
                    }
                    logger.LogError(ex, "[MIGRATIONS] Unexpected error attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);
                }
            }

            if (!useInMemoryRuntime && lastException != null)
            {
                logger.LogError(lastException, "[MIGRATIONS] Final failure after {MaxAttempts} attempts", maxAttempts);
                if (isAzureAd)
                {
                    logger.LogError("[MIGRATIONS] Potential causes: missing DB user for managed identity, AAD admin propagation delay, or firewall.");
                }
                if (app.Environment.IsDevelopment())
                {
                    logger.LogInformation("[MIGRATIONS] Development mode fallback: EnsureCreated()");
                    context.Database.EnsureCreated();
                }
                else
                {
                    throw lastException;
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