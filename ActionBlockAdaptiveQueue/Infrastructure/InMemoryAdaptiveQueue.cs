using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ActionBlockAdaptiveQueue.Domain;
using ActionBlockAdaptiveQueue.Domain.IAdaptiveQueue;

namespace ActionBlockAdaptiveQueue.Infrastructure;

/// <summary>
/// Represents an in-memory queue using ActionBlock,
/// reconfigured on the fly via IOptionsMonitor.
/// </summary>
public class InMemoryAdaptiveQueue<TJob> : IAdaptiveQueue<TJob> where TJob : IJob
{
    private readonly IOptionsMonitor<AdaptiveQueueOptions> _optionsMonitor;
    private readonly ILogger<InMemoryAdaptiveQueue<TJob>> _logger;

    private volatile ActionBlock<TJob> _actionBlock;
    private CancellationToken _cancellationToken;
    private bool _disposed;
    
    public InMemoryAdaptiveQueue(IOptionsMonitor<AdaptiveQueueOptions> optionsMonitor,
        ILogger<InMemoryAdaptiveQueue<TJob>> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Watch for options changes and update ActionBlock.
        _optionsMonitor.OnChange(OnOptionsChanged);
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        RecreateActionBlock();
        _logger.LogInformation("Queue started with concurrency={0}, capacity={1}",
            _optionsMonitor.CurrentValue.MaxDegreeOfParallelism,
            _optionsMonitor.CurrentValue.BoundedCapacity);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing the queue.");
        _actionBlock?.Complete();
        return Task.CompletedTask;
    }

    public bool TryEnqueue(TJob job)
    {
        if (job is null) throw new ArgumentNullException(nameof(job));

        bool success = _actionBlock.Post(job);
        _logger.LogDebug(success ? "Enqueued {JobId}" : "Queue full or not accepting: {JobId}", job.Id);
        return success;
    }

    public async Task<bool> TryEnqueueAsync(TJob job, CancellationToken ct)
    {
        if (job == null) throw new ArgumentNullException(nameof(job));

        bool success = await _actionBlock.SendAsync(job, ct);
        _logger.LogDebug(success ? "Enqueued {JobId}" : "Queue full or not accepting: {JobId}", job.Id);
        return success;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogInformation("Disposing queue...");
        await StopAsync(CancellationToken.None);

        _actionBlock = null;
    }

    private void OnOptionsChanged(AdaptiveQueueOptions newOptions)
    {
        _logger.LogInformation("Options changed, recreating ActionBlock...");
        RecreateActionBlock();
    }
    private void RecreateActionBlock()
    {
        // Create new block with updated concurrency/capacity.
        var options = _optionsMonitor.CurrentValue;
        var dataflowOptions = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
            BoundedCapacity = options.BoundedCapacity,
            CancellationToken = _cancellationToken
        };

        var newBlock = new ActionBlock<TJob>(async job =>
        {
            try
            {
                job.CancellationToken.ThrowIfCancellationRequested();
                _logger.LogDebug("Processing job {JobId}", job.Id);
                await job.ExecuteAsync(job.CancellationToken);
                job.TaskCompletionSource.SetResult(true);
            }
            catch (OperationCanceledException ex)
            {
                job.TaskCompletionSource.SetException(ex);
                _logger.LogWarning("Job canceled {JobId}", job.Id);
            }
            catch (Exception ex)
            {
                job.TaskCompletionSource.SetException(ex);
                _logger.LogError(ex, "Error processing {JobId}", job.Id);
            }
        }, dataflowOptions);

        // Swap old block out, if any
        var oldBlock = _actionBlock;
        _actionBlock = newBlock;
        oldBlock?.Complete();
    }
}