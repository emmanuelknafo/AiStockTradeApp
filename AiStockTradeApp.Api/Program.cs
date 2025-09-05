using AiStockTradeApp.DataAccess;
using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.DataAccess.Repositories;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Implementations;
using AiStockTradeApp.Services.Interfaces;
using AiStockTradeApp.Services;
using AiStockTradeApp.Api.Middleware;
using Azure.Identity;
using Azure.Core;
using AiStockTradeApp.Api; // diagnostics helpers
// AddAzureKeyVault extension lives in Azure.Extensions.AspNetCore.Configuration.Secrets
// Guard usage in case package isn't restored yet during early build pipeline phases.
// Note: Azure.Extensions.AspNetCore.Configuration.Secrets package not reliably available here; implementing manual bootstrap.
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Text;
using AiStockTradeApp.Api.Background;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Determine early test/dev environment (need before full config composition)
var earlyEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? builder.Environment.EnvironmentName;
var earlyIsDevelopment = string.Equals(earlyEnvironment, "Development", StringComparison.OrdinalIgnoreCase);
var earlyIsTesting = string.Equals(earlyEnvironment, "Testing", StringComparison.OrdinalIgnoreCase);

// Azure Key Vault integration
// Updated logic: ALWAYS attempt bootstrap if an explicit Key Vault name/URI is provided (even in Development),
// except in Testing where we intentionally isolate for deterministic tests.
// This enables "Dev" or "Development" slots in Azure to still hydrate secrets when configured.
try
{
    var kvUri = builder.Configuration["KeyVault:Uri"] ?? Environment.GetEnvironmentVariable("KEYVAULT_URI");
    var kvName = builder.Configuration["KeyVault:Name"] ?? Environment.GetEnvironmentVariable("KEYVAULT_NAME");
    if (string.IsNullOrWhiteSpace(kvUri) && !string.IsNullOrWhiteSpace(kvName)) kvUri = $"https://{kvName}.vault.azure.net/";

    var explicitKvConfig = !string.IsNullOrWhiteSpace(kvUri) || !string.IsNullOrWhiteSpace(kvName);

    if (!earlyIsTesting && ((!earlyIsDevelopment && !earlyIsTesting) || explicitKvConfig))
    {
        if (!string.IsNullOrWhiteSpace(kvUri))
        {
            var userAssignedClientId = builder.Configuration["ManagedIdentity:ClientId"] ?? Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            var credOpts = new DefaultAzureCredentialOptions();
            if (!string.IsNullOrWhiteSpace(userAssignedClientId)) credOpts.ManagedIdentityClientId = userAssignedClientId;
            var credential = new DefaultAzureCredential(credOpts);
            builder.Services.AddSingleton<TokenCredential>(credential);

            var secretClient = new Azure.Security.KeyVault.Secrets.SecretClient(new Uri(kvUri), credential);
            var secretsList = new List<string>();
            for (int i = 0; i < 20; i++)
            {
                var name = builder.Configuration[$"KeyVault:Secrets:{i}"]; if (string.IsNullOrWhiteSpace(name)) break; secretsList.Add(name.Trim());
            }
            if (secretsList.Count == 0)
            {
                secretsList.AddRange(new[] { "AlphaVantage--ApiKey", "TwelveData--ApiKey" });
            }
            var loaded = 0;
            var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var secretName in secretsList.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var secret = secretClient.GetSecret(secretName);
                    var value = secret.Value.Value;
                    if (value != null)
                    {
                        var key = secretName.Replace("--", ":");
                        dict[key] = value;
                        loaded++;
                    }
                }
                catch (Exception exSecret)
                {
                    Console.WriteLine($"[KeyVault][Info] Unable to load secret '{secretName}': {exSecret.Message}");
                }
            }
            if (loaded > 0)
            {
                builder.Configuration.AddInMemoryCollection(dict!);
                Console.WriteLine($"[KeyVault] Bootstrapped {loaded} secrets into configuration (Env={earlyEnvironment}, ExplicitConfig={explicitKvConfig}, UserAssigned={(string.IsNullOrWhiteSpace(userAssignedClientId)?"false":"true")}).");
            }
            else
            {
                Console.WriteLine($"[KeyVault] Attempted bootstrap but no secrets loaded (Env={earlyEnvironment}). Check access policies / secret names.");
            }
        }
        else
        {
            Console.WriteLine($"[KeyVault] Explicit bootstrap requested but no KeyVault:Uri / KEYVAULT_URI resolved (Env={earlyEnvironment}).");
        }
    }
    else
    {
        Console.WriteLine($"[KeyVault] Skipped (Environment={earlyEnvironment}, ExplicitKvConfig={explicitKvConfig}, Testing={earlyIsTesting}). Set KEYVAULT_URI to force load.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[KeyVault][Warn] Bootstrap failed: {ex.Message}");
}

// Enhanced logging configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Check if we're in a testing environment (multiple ways to detect this)
var isTesting = builder.Environment.EnvironmentName == "Testing" ||
                string.Equals(builder.Configuration["ASPNETCORE_ENVIRONMENT"], "Testing", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Testing", StringComparison.OrdinalIgnoreCase);

// EF Core for caching
var useInMemory =
    string.Equals(builder.Configuration["USE_INMEMORY_DB"], "true", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(Environment.GetEnvironmentVariable("USE_INMEMORY_DB"), "true", StringComparison.OrdinalIgnoreCase) ||
    isTesting;

if (useInMemory)
{
    builder.Services.AddDbContext<StockDataContext>(options => options.UseInMemoryDatabase("ApiCacheDb"));
}
else
{
    // Managed Identity only: obtain connection string directly from configuration / app settings
    var cs = builder.Configuration.GetConnectionString("DefaultConnection")
             ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
             ?? "Server=.;Database=StockTraderDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";

    ConnectionStringDiagnostics.Reset();
    ConnectionStringDiagnostics.Source = "Plain:Config";
    ConnectionStringDiagnostics.Resolved = true;
    ConnectionStringDiagnostics.RawHadKeyVaultToken = cs.Contains("@Microsoft.KeyVault", StringComparison.OrdinalIgnoreCase);

    builder.Services.AddDbContext<StockDataContext>(options =>
    {
        options.UseSqlServer(cs, sql =>
        {
            sql.EnableRetryOnFailure();
            sql.MigrationsAssembly(typeof(StockDataContext).Assembly.FullName);
        });
    });

    try
    {
        static string SanitizeForLog(string connectionString)
        {
            try
            {
                var b = new SqlConnectionStringBuilder(connectionString);
                if (b.ContainsKey("Password")) b.Password = "***";
                if (b.ContainsKey("User ID"))
                {
                    var uid = b["User ID"]?.ToString();
                    if (!string.IsNullOrEmpty(uid) && uid!.Length > 1)
                        b["User ID"] = uid[0] + "***";
                }
                if (b.TryGetValue("Access Token", out var _))
                {
                    b["Access Token"] = "***";
                }
                return b.ConnectionString;
            }
            catch
            {
                return "[unparseable-connection-string]";
            }
        }

        var unresolvedRef = cs.Contains("@Microsoft.KeyVault", StringComparison.OrdinalIgnoreCase);
        var sanitized = SanitizeForLog(cs);
        Console.WriteLine($"[ConnString] Final (sanitized) connection string selected. UnresolvedKeyVaultRef={unresolvedRef}. Value='{sanitized}'");
        if (unresolvedRef)
        {
            Console.WriteLine("[ConnString][Warning] Connection string still contains @Microsoft.KeyVault token. Ensure it is replaced with the full Managed Identity connection string.");
        }
        ConnectionStringDiagnostics.Sanitized = sanitized;
        ConnectionStringDiagnostics.Unresolved = unresolvedRef;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ConnString][Error] Failed to log sanitized connection string: {ex.Message}");
    }
}

// Data access + domain services
builder.Services.AddScoped<IStockDataRepository, StockDataRepository>();
builder.Services.AddScoped<IListedStockRepository, ListedStockRepository>();
builder.Services.AddScoped<IHistoricalPriceRepository, HistoricalPriceRepository>();
builder.Services.AddHttpClient<IStockDataService, StockDataService>();
builder.Services.AddScoped<IStockDataService, StockDataService>();
builder.Services.AddScoped<IListedStockService, ListedStockService>();
builder.Services.AddScoped<IHistoricalPriceService, HistoricalPriceService>();
builder.Services.AddScoped<IMockStockDataService, MockStockDataService>(); // Mock data fallback for when APIs fail
builder.Services.AddSingleton<IImportJobQueue, ImportJobQueue>();
builder.Services.AddHostedService<ImportJobProcessor>();

// Configure Data Protection for container persistence only
// This ensures any protected data remains valid across container restarts
// For local development, skip persistent data protection to avoid file system issues
var isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
                  !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH"));

if (isContainer)
{
    var dataProtectionKeysPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH") 
        ?? builder.Configuration["DataProtection:KeysPath"] 
        ?? "/app/keys"; // Default container path

    try
    {
        var keysDirectory = new DirectoryInfo(dataProtectionKeysPath);
        if (!keysDirectory.Exists)
        {
            keysDirectory.Create();
            Console.WriteLine($"Created data protection keys directory: {dataProtectionKeysPath}");
        }

        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(keysDirectory)
            .SetApplicationName("AiStockTradeApp") // Must be same across all instances
            .SetDefaultKeyLifetime(TimeSpan.FromDays(90)); // Keys valid for 90 days

        Console.WriteLine($"Data protection configured with persistent keys at: {dataProtectionKeysPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Failed to configure persistent data protection keys: {ex.Message}");
        Console.WriteLine("Using default in-memory data protection");
    }
}
else
{
    Console.WriteLine("Local development mode: Using default in-memory data protection");
}

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("StockUi", p =>
        p.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true));
});

// Only add Application Insights and Swagger for non-testing environments
if (!isTesting)
{
    try
    {
        builder.Services.AddApplicationInsightsTelemetry();
        builder.Services.AddSingleton<Microsoft.ApplicationInsights.TelemetryClient>(sp =>
        {
            var config = sp.GetRequiredService<Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration>();
            return new Microsoft.ApplicationInsights.TelemetryClient(config);
        });
    }
    catch
    {
        // Ignore Application Insights failures
    }

    try
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "AI Stock Trade API",
                Version = "v1",
                Description = "API for AI-powered stock tracking and analysis"
            });
            
            var xmlFile = Path.Combine(AppContext.BaseDirectory, "AiStockTradeApp.Api.xml");
            if (File.Exists(xmlFile))
            {
                c.IncludeXmlComments(xmlFile, true);
            }
        });
    }
    catch
    {
        // Ignore Swagger setup failures
    }
}

var app = builder.Build();

// Skip database migrations during testing
if (!useInMemory)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbMigration");
        var db = scope.ServiceProvider.GetRequiredService<StockDataContext>();
        
        var cs = db.Database.GetConnectionString();
        if (!string.IsNullOrWhiteSpace(cs))
        {
            var builderCs = new SqlConnectionStringBuilder(cs);
            var targetDb = builderCs.InitialCatalog;
            builderCs.InitialCatalog = "master";
            using var con = new SqlConnection(builderCs.ConnectionString);
            await con.OpenAsync();
            using var cmd = con.CreateCommand();
            cmd.CommandText = $"IF DB_ID('{targetDb.Replace("'", "''")}') IS NULL CREATE DATABASE [{targetDb}]";
            await cmd.ExecuteNonQueryAsync();
            logger.LogInformation("Ensured database '{Db}' exists.", targetDb);
        }

        await db.Database.MigrateAsync();
        logger.LogInformation("EF Core migrations applied successfully.");
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DbMigration");
        logger.LogError(ex, "Database migration failed, but continuing startup");
    }
}

app.UseHttpsRedirection();
app.UseCors("StockUi");

// Health check
app.MapGet("/health", () => Results.Ok("OK"));

// Extended database / configuration health
app.MapGet("/health/db", async (IServiceProvider sp) =>
{
    var response = new Dictionary<string, object?>();
    var overallStatus = "Healthy";

    bool useInMemoryDb = false;
    bool dbConnectionOk = false;
    int pendingMigrations = -1;
    string? dbError = null;
    TimeSpan? connectTime = null;
    long? listedStocks = null;
    long? historicalPrices = null;
    long? stockData = null;

    try
    {
        using var scope = sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<StockDataContext>();
        useInMemoryDb = ctx.Database.IsInMemory();

        if (!useInMemoryDb)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await ctx.Database.OpenConnectionAsync();
                dbConnectionOk = true;
            }
            catch (Exception ex)
            {
                dbError = ex.GetBaseException().Message;
                overallStatus = "Unhealthy";
            }
            finally
            {
                sw.Stop();
                connectTime = sw.Elapsed;
                try { await ctx.Database.CloseConnectionAsync(); } catch { }
            }

            try
            {
                var pending = await ctx.Database.GetPendingMigrationsAsync();
                pendingMigrations = pending.Count();
                if (pendingMigrations > 0 && overallStatus == "Healthy") overallStatus = "Degraded";
            }
            catch (Exception ex)
            {
                dbError ??= $"Pending migrations check failed: {ex.GetBaseException().Message}";
                if (overallStatus == "Healthy") overallStatus = "Degraded";
            }

            // Lightweight row counts (may be large; consider approximate future). Only run if connection succeeded.
            if (dbConnectionOk)
            {
                try { listedStocks = await ctx.ListedStocks.CountAsync(); } catch (Exception ex) { dbError ??= $"ListedStocks count failed: {ex.GetBaseException().Message}"; overallStatus = overallStatus == "Unhealthy" ? overallStatus : "Degraded"; }
                try { historicalPrices = await ctx.HistoricalPrices.Take(1).AnyAsync() ? await ctx.HistoricalPrices.CountAsync() : 0; } catch (Exception ex) { dbError ??= $"HistoricalPrices count failed: {ex.GetBaseException().Message}"; overallStatus = overallStatus == "Unhealthy" ? overallStatus : "Degraded"; }
                try { stockData = await ctx.StockData.CountAsync(); } catch (Exception ex) { dbError ??= $"StockData count failed: {ex.GetBaseException().Message}"; overallStatus = overallStatus == "Unhealthy" ? overallStatus : "Degraded"; }
            }
        }
        else
        {
            dbConnectionOk = true; // in-memory assumed healthy
            // In-memory: counts are cheap
            try { listedStocks = await ctx.ListedStocks.CountAsync(); } catch { }
            try { historicalPrices = await ctx.HistoricalPrices.CountAsync(); } catch { }
            try { stockData = await ctx.StockData.CountAsync(); } catch { }
        }
    }
    catch (Exception ex)
    {
        dbError = ex.GetBaseException().Message;
        overallStatus = "Unhealthy";
    }

    response["status"] = overallStatus;
    response["inMemory"] = useInMemoryDb;
    response["dbConnection"] = dbConnectionOk;
    response["connectMs"] = connectTime?.TotalMilliseconds;
    response["pendingMigrations"] = pendingMigrations;
    response["error"] = dbError;
    response["rowCounts"] = new {
        listedStocks,
        historicalPrices,
        stockData
    };
    response["connectionDiagnostics"] = new {
        ConnectionStringDiagnostics.Source,
        ConnectionStringDiagnostics.Resolved,
        ConnectionStringDiagnostics.Unresolved,
        ConnectionStringDiagnostics.AttemptedKeyVault,
        ConnectionStringDiagnostics.EnvFallbackUsed,
        ConnectionStringDiagnostics.RawHadKeyVaultToken
    };

    return Results.Json(response, statusCode: overallStatus == "Unhealthy" ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK);
})
.WithName("GetDatabaseHealth")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status503ServiceUnavailable);

// API key diagnostics with runtime Key Vault reference resolution
app.MapGet("/health/api-keys", async (IConfiguration config, ILogger<Program> logger, [FromServices] TokenCredential? credential) =>
{
    // TokenCredential is only registered when Key Vault bootstrap runs (Azure scenario). For local dev we allow null.
    var payload = await ApiKeyDiagnostics.GetStatusAsync(config, logger, credential);
    return Results.Json(payload);
})
.WithName("GetApiKeyHealth")
.Produces(StatusCodes.Status200OK);

// Configure the HTTP request pipeline - ONLY add Swagger UI if not testing AND has swagger services
if (app.Environment.IsDevelopment() && !isTesting)
{
    var swaggerProvider = app.Services.GetService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>();
    if (swaggerProvider != null)
    {
        try
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Stock Trade API v1");
                c.RoutePrefix = string.Empty;
            });
        }
        catch
        {
            // Ignore Swagger UI setup failures
        }
    }
}

// Simplified endpoints for all environments
app.MapGet("/api/stocks/quote", async ([FromQuery] string symbol, IStockDataService svc, ILogger<Program> logger) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return Results.BadRequest(new { error = "Symbol is required" });

        var result = await svc.GetStockQuoteAsync(symbol);
        return !result.Success 
            ? Results.NotFound(new { error = result.ErrorMessage ?? "Not found" }) 
            : Results.Ok(result.Data);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error getting stock quote for {Symbol}", symbol);
        return Results.Problem("Internal server error");
    }
})
.WithName("GetStockQuote")
.Produces<StockData>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/stocks/historical", async ([FromQuery] string symbol, [FromQuery] int days, IStockDataService svc, ILogger<Program> logger) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return Results.BadRequest(new { error = "Symbol is required" });

        days = days <= 0 ? 30 : days;
        var data = await svc.GetHistoricalDataAsync(symbol, days);
        return Results.Ok(data);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error getting historical data for {Symbol}", symbol);
        return Results.Problem("Internal server error");
    }
})
.WithName("GetHistoricalData")
.Produces<List<ChartDataPoint>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapGet("/api/stocks/suggestions", async ([FromQuery] string query, IStockDataService svc, ILogger<Program> logger) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(query))
            return Results.Ok(new List<string>());

        var list = await svc.GetStockSuggestionsAsync(query);
        return Results.Ok(list);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error getting stock suggestions for {Query}", query);
        return Results.Ok(new List<string>());
    }
})
.WithName("GetStockSuggestions")
.Produces<List<string>>(StatusCodes.Status200OK);

// Additional endpoints (historical prices, listed stocks, etc.)
app.MapGet("/api/historical-prices/{symbol}", async (string symbol, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? take, IHistoricalPriceService svc) =>
{
    if (string.IsNullOrWhiteSpace(symbol))
        return Results.BadRequest(new { error = "Symbol is required" });
    var list = await svc.GetAsync(symbol, from, to, take);
    return Results.Ok(list);
})
.WithName("GetHistoricalPrices")
.Produces<List<HistoricalPrice>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapGet("/api/historical-prices/count", async (IHistoricalPriceService svc) =>
{
    var total = await svc.CountAsync();
    return Results.Ok(total);
})
.WithName("GetHistoricalPricesCount")
.Produces<long>(StatusCodes.Status200OK);

app.MapGet("/api/historical-prices/{symbol}/count", async (string symbol, IHistoricalPriceService svc) =>
{
    if (string.IsNullOrWhiteSpace(symbol)) return Results.BadRequest(new { error = "Symbol is required" });
    var count = await svc.CountAsync(symbol);
    return Results.Ok(count);
})
.WithName("GetHistoricalPricesCountBySymbol")
.Produces<long>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapGet("/api/listed-stocks", async ([FromQuery] int skip, [FromQuery] int take, IListedStockService svc) =>
{
    skip = Math.Max(0, skip);
    take = take <= 0 || take > 2000 ? 500 : take;
    var list = await svc.GetAllAsync(skip, take);
    return Results.Ok(list);
})
.WithName("GetListedStocks")
.Produces<List<ListedStock>>(StatusCodes.Status200OK);

app.MapGet("/api/listed-stocks/count", async (IListedStockService svc) => Results.Ok(await svc.CountAsync()))
.WithName("GetListedStocksCount")
.Produces<int>(StatusCodes.Status200OK);

app.MapGet("/api/listed-stocks/{symbol}", async (string symbol, IListedStockService svc) =>
{
    var s = await svc.GetAsync(symbol);
    return s is null ? Results.NotFound() : Results.Ok(s);
})
.WithName("GetListedStock")
.Produces<ListedStock>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

app.MapPost("/api/listed-stocks", async ([FromBody] ListedStock stock, IListedStockService svc) =>
{
    if (string.IsNullOrWhiteSpace(stock.Symbol) || string.IsNullOrWhiteSpace(stock.Name))
        return Results.BadRequest(new { error = "Symbol and Name are required" });
    stock.Symbol = stock.Symbol.ToUpperInvariant();
    stock.UpdatedAt = DateTime.UtcNow;
    await svc.UpsertAsync(stock);
    return Results.Ok(stock);
})
.WithName("UpsertListedStock")
.Produces<ListedStock>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapPost("/api/listed-stocks/bulk", async ([FromBody] IEnumerable<ListedStock> stocks, IListedStockService svc) =>
{
    foreach (var s in stocks)
    {
        if (!string.IsNullOrWhiteSpace(s.Symbol)) s.Symbol = s.Symbol.ToUpperInvariant();
        s.UpdatedAt = DateTime.UtcNow;
    }
    await svc.BulkUpsertAsync(stocks);
    return Results.Ok(new { count = stocks.Count() });
})
.WithName("BulkUpsertListedStocks")
.Produces(StatusCodes.Status200OK);

app.MapGet("/api/listed-stocks/search", async ([FromQuery] string? q, [FromQuery] string? sector, [FromQuery] string? industry, [FromQuery] int skip, [FromQuery] int take, IListedStockService svc) =>
{
    skip = Math.Max(0, skip);
    take = take <= 0 || take > 2000 ? 500 : take;
    var list = await svc.SearchAsync(sector, industry, q, skip, take);
    var total = await svc.SearchCountAsync(sector, industry, q);
    return Results.Ok(new { total, items = list });
})
.WithName("SearchListedStocks")
.Produces(StatusCodes.Status200OK);

// Facets for filtering UI
app.MapGet("/api/listed-stocks/facets/sectors", async (IListedStockService svc) => Results.Ok(await svc.GetDistinctSectorsAsync()))
.WithName("GetSectorsFacet")
.Produces<List<string>>(StatusCodes.Status200OK);
app.MapGet("/api/listed-stocks/facets/industries", async (IListedStockService svc) => Results.Ok(await svc.GetDistinctIndustriesAsync()))
.WithName("GetIndustriesFacet")
.Produces<List<string>>(StatusCodes.Status200OK);

// Import screener CSV (text/csv or text/plain body) - enqueue background job, return 202
app.MapPost("/api/listed-stocks/import-csv", async (HttpRequest req, IImportJobQueue queue, ILogger<Program> logger) =>
{
    var correlationId = req.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString("N")[..8];
    
    return await logger.LogApiOperationAsync(
        "ImportListedStocksCsv",
        async () =>
        {
            using var reader = new StreamReader(req.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            var content = await reader.ReadToEndAsync();
            
            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogValidationError("ImportListedStocksCsv", "body", "[empty]", "Empty body", correlationId);
                return Results.BadRequest(new { error = "Empty body" });
            }

            var fileName = req.Headers.ContainsKey("X-File-Name") ? req.Headers["X-File-Name"].ToString() : null;
            var contentLength = content.Length;
            var lineCount = content.Split('\n').Length;
            
            var job = new ImportJob
            {
                Content = content,
                SourceName = fileName
            };
            
            var status = queue.Enqueue(job);
            var location = $"/api/listed-stocks/import-jobs/{status.Id}";
            
            logger.LogImportJobProgress(status.Id, "ListedStocksCsv", status.Status.ToString());
            logger.LogBusinessEvent("ImportJobCreated", new { 
                jobId = status.Id, 
                fileName, 
                contentLength, 
                lineCount,
                correlationId 
            });
            
            return Results.Accepted(location, new { jobId = status.Id, status = status.Status.ToString(), location });
        },
        new { fileName = req.Headers["X-File-Name"].ToString() },
        correlationId);
})
.WithName("ImportListedStocksCsv")
.Produces(StatusCodes.Status202Accepted)
.Produces(StatusCodes.Status400BadRequest);

// Import historical prices CSV for a symbol
app.MapPost("/api/historical-prices/{symbol}/import-csv", async (string symbol, HttpRequest req, IImportJobQueue queue) =>
{
    using var reader = new StreamReader(req.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
    var content = await reader.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(content))
        return Results.BadRequest(new { error = "Empty body" });

    var job = new ImportJob
    {
        Content = content,
        SourceName = req.Headers.ContainsKey("X-File-Name") ? req.Headers["X-File-Name"].ToString() : null,
        Type = "HistoricalPricesCsv",
        Symbol = symbol
    };
    var status = queue.Enqueue(job);
    var location = $"/api/listed-stocks/import-jobs/{status.Id}";
    return Results.Accepted(location, new { jobId = status.Id, status = status.Status.ToString(), location });
})
.WithName("ImportHistoricalPricesCsv")
.Produces(StatusCodes.Status202Accepted)
.Produces(StatusCodes.Status400BadRequest);

// Get job status
app.MapGet("/api/listed-stocks/import-jobs/{id}", (Guid id, IImportJobQueue queue) =>
{
    return queue.TryGetStatus(id, out var status) && status != null
        ? Results.Ok(new
        {
            jobId = status.Id,
            status = status.Status.ToString(),
            createdAt = status.CreatedAt,
            startedAt = status.StartedAt,
            completedAt = status.CompletedAt,
            total = status.TotalItems,
            processed = status.ProcessedItems,
            error = status.Error
        })
        : Results.NotFound();
})
.WithName("GetImportJobStatus")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

app.MapDelete("/api/listed-stocks", async (IListedStockService svc) =>
{
    await svc.DeleteAllAsync();
    return Results.NoContent();
})
.WithName("DeleteAllListedStocks")
.Produces(StatusCodes.Status204NoContent);

// Seed ListedStocks from embedded CSV on first run if empty
if (!isTesting)
{
    try
    {
        await SeedListedStocksIfEmptyAsync(app.Services);
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");
        logger.LogError(ex, "Error during seeding, but continuing startup");
    }
}

app.Run();

static async Task SeedListedStocksIfEmptyAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");
    var db = scope.ServiceProvider.GetRequiredService<StockDataContext>();
    var svc = scope.ServiceProvider.GetRequiredService<IListedStockService>();

    try
    {
        var count = await db.ListedStocks.CountAsync();
        if (count > 0)
        {
            logger.LogInformation("ListedStocks already seeded: {Count} rows.", count);
            return;
        }

        var baseDir = AppContext.BaseDirectory;
        var seedDir = Path.Combine(baseDir, "SeedData");
        if (!Directory.Exists(seedDir))
        {
            logger.LogWarning("Seed directory not found: {Dir}", seedDir);
            return;
        }

        var files = Directory.GetFiles(seedDir, "*.csv");
        if (files.Length == 0)
        {
            logger.LogWarning("No seed CSV files found in {Dir}", seedDir);
            return;
        }

        var seedFile = files.OrderByDescending(File.GetLastWriteTimeUtc).First();
        logger.LogInformation("Seeding ListedStocks from {File}...", Path.GetFileName(seedFile));

        var lines = await File.ReadAllLinesAsync(seedFile);
        if (lines.Length == 0) return;

        var start = lines[0].StartsWith("Symbol,", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var buffer = new List<ListedStock>(capacity: 1000);

        for (int i = start; i < lines.Length; i++)
        {
            var cols = SplitCsvLine(lines[i]);
            if (cols.Count < 11) continue;
            
            try
            {
                var stock = new ListedStock
                {
                    Symbol = (cols[0] ?? string.Empty).Trim().ToUpperInvariant(),
                    Name = (cols[1] ?? string.Empty).Trim(),
                    LastSale = ToDecimal(cols[2]),
                    NetChange = ToDecimal(cols[3]),
                    PercentChange = ToPercent(cols[4]),
                    MarketCap = ToDecimal(cols[5]),
                    Country = NullIfEmpty(cols[6]),
                    IpoYear = ToNullableInt(cols[7]),
                    Volume = ToLong(cols[8]),
                    Sector = NullIfEmpty(cols[9]),
                    Industry = NullIfEmpty(cols[10]),
                    UpdatedAt = DateTime.UtcNow,
                };
                if (!string.IsNullOrWhiteSpace(stock.Symbol) && !string.IsNullOrWhiteSpace(stock.Name))
                    buffer.Add(stock);
            }
            catch { }
        }

        if (buffer.Count == 0) return;

        const int batchSize = 500;
        for (int i = 0; i < buffer.Count; i += batchSize)
        {
            var batch = buffer.Skip(i).Take(batchSize).ToArray();
            await svc.BulkUpsertAsync(batch);
        }
        logger.LogInformation("Completed seeding ListedStocks: {Total} records.", buffer.Count);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error seeding ListedStocks.");
    }

    static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int j = 0; j < line.Length; j++)
        {
            var ch = line[j];
            if (ch == '"')
            {
                if (inQuotes && j + 1 < line.Length && line[j + 1] == '"')
                {
                    sb.Append('"');
                    j++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }
        result.Add(sb.ToString());
        return result;
    }

    static decimal ToDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        s = s.Replace("$", string.Empty).Replace(",", string.Empty).Trim();
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }
    static decimal ToPercent(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        s = s.Replace("%", string.Empty).Trim();
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }
    static long ToLong(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0L;
        s = s.Replace(",", string.Empty).Trim();
        return long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0L;
    }
    static int? ToNullableInt(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return int.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
    static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

// Structured diagnostics helper for connection string resolution
static class ConnectionStringDiagnostics
{
    public static string? Sanitized { get; set; }
    public static bool Unresolved { get; set; }
    public static bool AttemptedKeyVault { get; set; }
    public static bool EnvFallbackUsed { get; set; }
    public static bool Resolved { get; set; }
    public static bool RawHadKeyVaultToken { get; set; }
    public static string? Source { get; set; }

    public static void Reset()
    {
        Sanitized = null;
        Unresolved = false;
        AttemptedKeyVault = false;
        EnvFallbackUsed = false;
        Resolved = false;
        RawHadKeyVaultToken = false;
        Source = null;
    }
}
