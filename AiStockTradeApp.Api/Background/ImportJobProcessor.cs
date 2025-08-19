using System.Globalization;
using System.Text;
using AiStockTradeApp.Api.Background;
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
        _logger.LogInformation("Import job processor started.");
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
                _logger.LogError(ex, "Error reading from job queue");
            }
        }
        _logger.LogInformation("Import job processor stopping.");
    }

    private async Task ProcessJobAsync(ImportJob job, CancellationToken ct)
    {
        if (_queue is ImportJobQueue impl)
        {
            impl.UpdateProgress(job.Id, s => { s.Status = JobStatus.Processing; s.StartedAt = DateTime.UtcNow; });
        }

        try
        {
            using var scope = _services.CreateScope();
            if (string.Equals(job.Type, "HistoricalPricesCsv", StringComparison.OrdinalIgnoreCase))
            {
                var historicalSvc = scope.ServiceProvider.GetRequiredService<IHistoricalPriceService>();
                var lines = job.Content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var start = 0;
                if (lines.Length > 0 && lines[0].StartsWith("Date,", StringComparison.OrdinalIgnoreCase)) start = 1;

                var total = Math.Max(0, lines.Length - start);
                if (_queue is ImportJobQueue impl2) impl2.UpdateProgress(job.Id, s => s.TotalItems = total);

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
                    catch { /* ignore malformed row */ }

                    if (buffer.Count >= batchSize)
                    {
                        await historicalSvc.UpsertManyAsync(buffer);
                        processed += buffer.Count;
                        buffer.Clear();
                        if (_queue is ImportJobQueue impl3) impl3.UpdateProgress(job.Id, s => s.ProcessedItems = processed);
                    }
                }

                if (buffer.Count > 0)
                {
                    await historicalSvc.UpsertManyAsync(buffer);
                    processed += buffer.Count;
                    if (_queue is ImportJobQueue impl4) impl4.UpdateProgress(job.Id, s => s.ProcessedItems = processed);
                }
            }
            else
            {
                var listedStockService = scope.ServiceProvider.GetRequiredService<IListedStockService>();

                // Parse CSV content (same logic as existing endpoint, but streaming-friendly)
                var lines = job.Content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var start = 0;
                if (lines.Length > 0 && lines[0].StartsWith("Symbol,", StringComparison.OrdinalIgnoreCase)) start = 1;

                var total = Math.Max(0, lines.Length - start);
                if (_queue is ImportJobQueue impl2) impl2.UpdateProgress(job.Id, s => s.TotalItems = total);

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
                    catch { /* ignore malformed row */ }

                    if (buffer.Count >= batchSize)
                    {
                        await listedStockService.BulkUpsertAsync(buffer);
                        processed += buffer.Count;
                        buffer.Clear();
                        if (_queue is ImportJobQueue impl3) impl3.UpdateProgress(job.Id, s => s.ProcessedItems = processed);
                    }
                }

                if (buffer.Count > 0)
                {
                    await listedStockService.BulkUpsertAsync(buffer);
                    processed += buffer.Count;
                    if (_queue is ImportJobQueue impl4) impl4.UpdateProgress(job.Id, s => s.ProcessedItems = processed);
                }
            }

            if (_queue is ImportJobQueue impl5)
            {
                impl5.UpdateProgress(job.Id, s => { s.Status = JobStatus.Completed; s.CompletedAt = DateTime.UtcNow; });
            }
        }
        catch (Exception ex)
        {
            if (_queue is ImportJobQueue impl6)
            {
                impl6.UpdateProgress(job.Id, s => { s.Status = JobStatus.Failed; s.Error = ex.Message; s.CompletedAt = DateTime.UtcNow; });
            }
        }
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
}
