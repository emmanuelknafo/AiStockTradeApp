using AiStockTradeApp.DataAccess;
using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.DataAccess.Repositories;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Implementations;
using AiStockTradeApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Text;
using AiStockTradeApp.Api.Background;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add Swagger services for UI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core for caching
var useInMemory = string.Equals(Environment.GetEnvironmentVariable("USE_INMEMORY_DB"), "true", StringComparison.OrdinalIgnoreCase);
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
            // Explicitly set migrations assembly to ensure discovery in containerized environments
            sql.MigrationsAssembly(typeof(StockDataContext).Assembly.FullName);
        });
    });
}

// Data access + domain services
builder.Services.AddScoped<IStockDataRepository, StockDataRepository>();
builder.Services.AddScoped<IListedStockRepository, ListedStockRepository>();
// HttpClient for external providers used by StockDataService
builder.Services.AddHttpClient<IStockDataService, StockDataService>();
builder.Services.AddScoped<IStockDataService, StockDataService>();
builder.Services.AddScoped<IListedStockService, ListedStockService>();
// Background job queue for long-running tasks
builder.Services.AddSingleton<IImportJobQueue, ImportJobQueue>();
builder.Services.AddHostedService<ImportJobProcessor>();

// CORS (optional; enable if UI is served from another origin)
builder.Services.AddCors(options =>
{
    options.AddPolicy("StockUi", p =>
        p.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    // Enable Swagger UI
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Stock Trade API v1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

// Create database if missing, then apply EF Core migrations on startup (with robust retry)
if (!useInMemory)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbMigration");
    var db = scope.ServiceProvider.GetRequiredService<StockDataContext>();
    try
    {
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
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed ensuring database exists; will rely on retries.");
    }

    var attempts = 0;
    var maxAttempts = 30; // allow more time for SQL Server to become ready in containers
    var delay = TimeSpan.FromSeconds(5);
    while (true)
    {
        try
        {
            logger.LogInformation("Applying EF Core migrations (attempt {Attempt}/{Max})...", attempts + 1, maxAttempts);
            await db.Database.MigrateAsync();
            logger.LogInformation("EF Core migrations applied successfully.");
            break;
        }
        catch (Exception ex)
        {
            attempts++;
            logger.LogWarning(ex, "Migration attempt {Attempt} failed.", attempts);
            if (attempts >= maxAttempts)
            {
                logger.LogCritical(ex, "Exceeded max migration attempts. Exiting.");
                // Fail fast so container restarts and retries until DB is ready
                throw;
            }
            await Task.Delay(delay);
        }
    }
}

app.UseHttpsRedirection();
app.UseCors("StockUi");

// Health check
app.MapGet("/health", () => Results.Ok("OK"));

// Map minimal API endpoints
app.MapGet("/api/stocks/quote", async ([FromQuery] string symbol, IStockDataService svc) =>
{
    if (string.IsNullOrWhiteSpace(symbol))
        return Results.BadRequest(new { error = "Symbol is required" });

    var result = await svc.GetStockQuoteAsync(symbol);
    if (!result.Success)
        return Results.NotFound(new { error = result.ErrorMessage ?? "Not found" });

    return Results.Ok(result.Data);
})
.WithName("GetStockQuote")
.Produces<StockData>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status404NotFound);

app.MapGet("/api/stocks/historical", async ([FromQuery] string symbol, [FromQuery] int days, IStockDataService svc) =>
{
    if (string.IsNullOrWhiteSpace(symbol))
        return Results.BadRequest(new { error = "Symbol is required" });

    days = days <= 0 ? 30 : days;
    var data = await svc.GetHistoricalDataAsync(symbol, days);
    return Results.Ok(data);
})
.WithName("GetHistoricalData")
.Produces<List<ChartDataPoint>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.MapGet("/api/stocks/suggestions", async ([FromQuery] string query, IStockDataService svc) =>
{
    if (string.IsNullOrWhiteSpace(query))
        return Results.Ok(new List<string>());

    var list = await svc.GetStockSuggestionsAsync(query);
    return Results.Ok(list);
})
.WithName("GetStockSuggestions")
.Produces<List<string>>(StatusCodes.Status200OK);

// Listed stocks catalog
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
app.MapPost("/api/listed-stocks/import-csv", async (HttpRequest req, IImportJobQueue queue) =>
{
    using var reader = new StreamReader(req.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
    var content = await reader.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(content))
        return Results.BadRequest(new { error = "Empty body" });

    var job = new ImportJob
    {
        Content = content,
        SourceName = req.Headers.ContainsKey("X-File-Name") ? req.Headers["X-File-Name"].ToString() : null
    };
    var status = queue.Enqueue(job);
    var location = $"/api/listed-stocks/import-jobs/{status.Id}";
    return Results.Accepted(location, new { jobId = status.Id, status = status.Status.ToString(), location });
})
.WithName("ImportListedStocksCsv")
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

app.Run();
