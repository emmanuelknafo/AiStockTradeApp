using AiStockTradeApp.DataAccess;
using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.DataAccess.Repositories;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Implementations;
using AiStockTradeApp.Services.Interfaces;
using AiStockTradeApp.Services;
using AiStockTradeApp.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Text;
using AiStockTradeApp.Api.Background;

var builder = WebApplication.CreateBuilder(args);

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
    builder.Services.AddDbContext<StockDataContext>(options =>
    {
        var cs = builder.Configuration.GetConnectionString("DefaultConnection")
                 ?? "Server=.;Database=StockTraderDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";
        options.UseSqlServer(cs, sql =>
        {
            sql.EnableRetryOnFailure();
            sql.MigrationsAssembly(typeof(StockDataContext).Assembly.FullName);
        });
    });
}

// Data access + domain services
builder.Services.AddScoped<IStockDataRepository, StockDataRepository>();
builder.Services.AddScoped<IListedStockRepository, ListedStockRepository>();
builder.Services.AddScoped<IHistoricalPriceRepository, HistoricalPriceRepository>();
builder.Services.AddHttpClient<IStockDataService, StockDataService>();
builder.Services.AddScoped<IStockDataService, StockDataService>();
builder.Services.AddScoped<IListedStockService, ListedStockService>();
builder.Services.AddScoped<IHistoricalPriceService, HistoricalPriceService>();
builder.Services.AddSingleton<IImportJobQueue, ImportJobQueue>();
builder.Services.AddHostedService<ImportJobProcessor>();

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
