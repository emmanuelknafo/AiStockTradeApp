using AiStockTradeApp.DataAccess;
using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.DataAccess.Repositories;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Implementations;
using AiStockTradeApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

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
        options.UseSqlServer(cs, sql => sql.EnableRetryOnFailure());
    });
}

// Data access + domain services
builder.Services.AddScoped<IStockDataRepository, StockDataRepository>();
// HttpClient for external providers used by StockDataService
builder.Services.AddHttpClient<IStockDataService, StockDataService>();
builder.Services.AddScoped<IStockDataService, StockDataService>();

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

// Create database if missing, then apply EF Core migrations on startup (with simple retry)
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
    var maxAttempts = 10;
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
                logger.LogError(ex, "Exceeded max migration attempts. Starting without successful migration.");
                break;
            }
            await Task.Delay(delay);
        }
    }
}

app.UseHttpsRedirection();
app.UseCors("StockUi");

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

app.Run();
