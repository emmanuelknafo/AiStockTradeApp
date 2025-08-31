using System.Globalization;
using System.Text;
using AiStockTradeApp.Api.Background;
using AiStockTradeApp.Api.Middleware;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Interfaces;
using AiStockTradeApp.Services.Implementations;

namespace AiStockTradeApp.Api.Background;

public sealed class ImportJobProcessor : BackgroundService
{
    private readonly ILogger<ImportJobProcessor> _logger;
    private readonly IImportJobQueue _queue;
    private readonly IServiceProvider _services;

    public ImportJobProcessor(ILogger<ImportJobProcessor> logger, IImportJobQueue queue, IServiceProvider services)
    {
        _logger = logger;
        _queue = queue;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _logger.LogApiOperationAsync(
            "ImportJobProcessor_Startup",
            () =>
            {
                _logger.LogInformation("Import job processor started with enhanced logging");
                return Task.CompletedTask;
            });

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(stoppingToken);
                _ = ProcessJobAsync(job, stoppingToken); // fire-and-forget within service scope
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from job queue - will continue processing");
            }
        }

        await _logger.LogApiOperationAsync(
            "ImportJobProcessor_Shutdown",
            () =>
            {
                _logger.LogInformation("Import job processor stopping gracefully");
                return Task.CompletedTask;
            });
    }

    private async Task ProcessJobAsync(ImportJob job, CancellationToken ct)
    {
        var jobCorrelationId = $"job-{job.Id}";
        
        await _logger.LogApiOperationAsync(
            "ProcessImportJob",
            async () =>
            {
                if (_queue is ImportJobQueue impl)
                {
                    impl.UpdateProgress(job.Id, s => { s.Status = JobStatus.Processing; s.StartedAt = DateTime.UtcNow; });
                    _logger.LogInformation("Job {JobId} status updated to Processing", job.Id);
                }

                try
                {
                    using var scope = _services.CreateScope();
                    
                    if (string.Equals(job.Type, "HistoricalPricesCsv", StringComparison.OrdinalIgnoreCase))
                    {
                        await ProcessHistoricalPricesJobAsync(job, scope, ct, jobCorrelationId);
                    }
                    else
                    {
                        await ProcessListedStocksJobAsync(job, scope, ct, jobCorrelationId);
                    }

                    if (_queue is ImportJobQueue impl5)
                    {
                        impl5.UpdateProgress(job.Id, s => { s.Status = JobStatus.Completed; s.CompletedAt = DateTime.UtcNow; });
                        _logger.LogInformation("Job {JobId} completed successfully", job.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Job {JobId} failed during processing - Type: {JobType}, Symbol: {Symbol}", 
                        job.Id, job.Type, job.Symbol);
                    
                    if (_queue is ImportJobQueue impl6)
                    {
                        impl6.UpdateProgress(job.Id, s => { s.Status = JobStatus.Failed; s.Error = ex.Message; s.CompletedAt = DateTime.UtcNow; });
                    }
                    throw; // Re-throw to ensure LogApiOperationAsync captures the failure
                }
                
                return Task.CompletedTask;
            },
            new { JobId = job.Id, JobType = job.Type, Symbol = job.Symbol, ContentLength = job.Content?.Length ?? 0 },
            jobCorrelationId);
    }

    private static List<string> ParseCsvLine(string line)
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
                    j++; // skip escaped quote
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

    private static decimal ToDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        s = s.Replace("$", string.Empty).Replace(",", string.Empty).Trim();
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static bool TryParseDate(string? s, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var formats = new[] { "M/d/yyyy", "MM/dd/yyyy", "yyyy-MM-dd" };
        return DateTime.TryParseExact(s.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date)
            || DateTime.TryParse(s.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
    }

    private static decimal ToPercent(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0m;
        s = s.Replace("%", string.Empty).Trim();
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static long ToLong(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0L;
        s = s.Replace(",", string.Empty).Trim();
        return long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0L;
    }

    private static int? ToNullableInt(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return int.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task ProcessHistoricalPricesJobAsync(ImportJob job, IServiceScope scope, CancellationToken ct, string correlationId)
    {
        await _logger.LogDatabaseOperationAsync(
            "ProcessHistoricalPricesJob",
            "HistoricalPrice",
            async () =>
            {
                var historicalSvc = scope.ServiceProvider.GetRequiredService<IHistoricalPriceService>();
                var lines = job.Content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var start = 0;
                if (lines.Length > 0 && lines[0].StartsWith("Date,", StringComparison.OrdinalIgnoreCase)) start = 1;

                var total = Math.Max(0, lines.Length - start);
                if (_queue is ImportJobQueue impl2) 
                {
                    impl2.UpdateProgress(job.Id, s => s.TotalItems = total);
                    _logger.LogInformation("Historical prices job {JobId} - Total items to process: {TotalItems}", job.Id, total);
                }

                const int batchSize = 500;
                var buffer = new List<HistoricalPrice>(batchSize);
                var processed = 0;
                var symbol = (job.Symbol ?? string.Empty).ToUpperInvariant();

                for (int i = start; i < lines.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var cols = ParseCsvLine(lines[i]);
                    if (cols.Count < 6) continue;
                    try
                    {
                        if (!TryParseDate(cols[0], out var date)) continue;
                        var close = ToDecimal(cols[1]);
                        var volume = ToLong(cols[2]);
                        var open = ToDecimal(cols[3]);
                        var high = ToDecimal(cols[4]);
                        var low = ToDecimal(cols[5]);
                        var item = new HistoricalPrice
                        {
                            Symbol = symbol,
                            Date = date,
                            Open = open,
                            High = high,
                            Low = low,
                            Close = close,
                            Volume = volume,
                            Source = job.SourceName
                        };
                        if (!string.IsNullOrWhiteSpace(item.Symbol)) buffer.Add(item);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse historical price row {RowIndex} for job {JobId}: {Row}", 
                            i, job.Id, lines[i]);
                    }

                    if (buffer.Count >= batchSize)
                    {
                        await _logger.LogDatabaseOperationAsync(
                            "UpsertHistoricalPricesBatch",
                            "HistoricalPrice",
                            async () =>
                            {
                                await historicalSvc.UpsertManyAsync(buffer);
                                return $"Upserted {buffer.Count} historical prices";
                            },
                            new { JobId = job.Id, BatchSize = buffer.Count, Symbol = symbol });

                        processed += buffer.Count;
                        buffer.Clear();
                        if (_queue is ImportJobQueue impl3) 
                        {
                            impl3.UpdateProgress(job.Id, s => s.ProcessedItems = processed);
                            _logger.LogDebug("Historical prices job {JobId} - Progress: {ProcessedItems}/{TotalItems}", 
                                job.Id, processed, total);
                        }
                    }
                }

                if (buffer.Count > 0)
                {
                    await _logger.LogDatabaseOperationAsync(
                        "UpsertHistoricalPricesFinal",
                        "HistoricalPrice",
                        async () =>
                        {
                            await historicalSvc.UpsertManyAsync(buffer);
                            return $"Upserted final {buffer.Count} historical prices";
                        },
                        new { JobId = job.Id, BatchSize = buffer.Count, Symbol = symbol });

                    processed += buffer.Count;
                    if (_queue is ImportJobQueue impl4) 
                    {
                        impl4.UpdateProgress(job.Id, s => s.ProcessedItems = processed);
                        _logger.LogInformation("Historical prices job {JobId} - Final progress: {ProcessedItems} items", 
                            job.Id, processed);
                    }
                }

                return $"Processed {processed} historical price records";
            },
            new { JobId = job.Id, Symbol = job.Symbol, TotalLines = job.Content?.Split('\n').Length ?? 0 });
    }

    private async Task ProcessListedStocksJobAsync(ImportJob job, IServiceScope scope, CancellationToken ct, string correlationId)
    {
        await _logger.LogDatabaseOperationAsync(
            "ProcessListedStocksJob",
            "ListedStock",
            async () =>
            {
                var listedStockService = scope.ServiceProvider.GetRequiredService<IListedStockService>();

                // Parse CSV content (same logic as existing endpoint, but streaming-friendly)
                var lines = job.Content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var start = 0;
                if (lines.Length > 0 && lines[0].StartsWith("Symbol,", StringComparison.OrdinalIgnoreCase)) start = 1;

                var total = Math.Max(0, lines.Length - start);
                if (_queue is ImportJobQueue impl2) 
                {
                    impl2.UpdateProgress(job.Id, s => s.TotalItems = total);
                    _logger.LogInformation("Listed stocks job {JobId} - Total items to process: {TotalItems}", job.Id, total);
                }

                // Batch upserts to reduce DB roundtrips
                const int batchSize = 500;
                var buffer = new List<ListedStock>(batchSize);
                var processed = 0;

                for (int i = start; i < lines.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var cols = ParseCsvLine(lines[i]);
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
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse listed stock row {RowIndex} for job {JobId}: {Row}", 
                            i, job.Id, lines[i]);
                    }

                    if (buffer.Count >= batchSize)
                    {
                        await _logger.LogDatabaseOperationAsync(
                            "BulkUpsertListedStocksBatch",
                            "ListedStock",
                            async () =>
                            {
                                await listedStockService.BulkUpsertAsync(buffer);
                                return $"Bulk upserted {buffer.Count} listed stocks";
                            },
                            new { JobId = job.Id, BatchSize = buffer.Count });

                        processed += buffer.Count;
                        buffer.Clear();
                        if (_queue is ImportJobQueue impl3) 
                        {
                            impl3.UpdateProgress(job.Id, s => s.ProcessedItems = processed);
                            _logger.LogDebug("Listed stocks job {JobId} - Progress: {ProcessedItems}/{TotalItems}", 
                                job.Id, processed, total);
                        }
                    }
                }

                if (buffer.Count > 0)
                {
                    await _logger.LogDatabaseOperationAsync(
                        "BulkUpsertListedStocksFinal",
                        "ListedStock",
                        async () =>
                        {
                            await listedStockService.BulkUpsertAsync(buffer);
                            return $"Bulk upserted final {buffer.Count} listed stocks";
                        },
                        new { JobId = job.Id, BatchSize = buffer.Count });

                    processed += buffer.Count;
                    if (_queue is ImportJobQueue impl4) 
                    {
                        impl4.UpdateProgress(job.Id, s => s.ProcessedItems = processed);
                        _logger.LogInformation("Listed stocks job {JobId} - Final progress: {ProcessedItems} items", 
                            job.Id, processed);
                    }
                }

                return $"Processed {processed} listed stock records";
            },
            new { JobId = job.Id, TotalLines = job.Content?.Split('\n').Length ?? 0 });
    }
}
