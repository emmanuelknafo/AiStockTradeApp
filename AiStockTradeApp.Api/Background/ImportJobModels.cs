using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AiStockTradeApp.Api.Background;

public enum JobStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}

public sealed class ImportJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Type { get; init; } = "ListedStocksCsv";
    public string? SourceName { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Content { get; init; } = string.Empty;
}

public sealed class ImportJobStatus
{
    public Guid Id { get; init; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public string? Error { get; set; }
}

public interface IImportJobQueue
{
    ImportJobStatus Enqueue(ImportJob job);
    ValueTask<ImportJob> DequeueAsync(CancellationToken cancellationToken);
    bool TryGetStatus(Guid id, out ImportJobStatus? status);
    IReadOnlyCollection<ImportJobStatus> GetAll();
}

public sealed class ImportJobQueue : IImportJobQueue
{
    private readonly Channel<ImportJob> _queue = Channel.CreateUnbounded<ImportJob>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private readonly ConcurrentDictionary<Guid, ImportJobStatus> _statuses = new();

    public ImportJobStatus Enqueue(ImportJob job)
    {
        var status = new ImportJobStatus { Id = job.Id, Status = JobStatus.Queued, CreatedAt = job.CreatedAt };
        _statuses[job.Id] = status;
        _queue.Writer.TryWrite(job);
        return status;
    }

    public async ValueTask<ImportJob> DequeueAsync(CancellationToken cancellationToken)
        => await _queue.Reader.ReadAsync(cancellationToken);

    public bool TryGetStatus(Guid id, out ImportJobStatus? status)
        => _statuses.TryGetValue(id, out status);

    public IReadOnlyCollection<ImportJobStatus> GetAll()
        => _statuses.Values.ToArray();

    internal void UpdateProgress(Guid id, Action<ImportJobStatus> update)
    {
        if (_statuses.TryGetValue(id, out var current))
        {
            update(current);
        }
    }
}
