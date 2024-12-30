using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ActionBlockAdaptiveQueue.Domain.AdaptiveQueue;
using ActionBlockAdaptiveQueue.Domain.Job;

namespace ActionBlockAdaptiveQueue.Infrastructure;

/// <summary>
/// Represents an in-memory adaptive queue that processes jobs using an <see cref="ActionBlock{T}"/>.
/// The queue dynamically reconfigures its processing options at runtime based on changes detected
/// through <see cref="IOptionsMonitor{TOptions}"/>.
/// </summary>
/// <typeparam name="TJob">The type of job to be processed. Must implement <see cref="IJob"/>.</typeparam>
public class InMemoryAdaptiveQueue<TJob> : IAdaptiveQueue<TJob> where TJob : IJob
{
    private readonly IOptionsMonitor<AdaptiveQueueOptions> _optionsMonitor;
    private readonly ILogger<InMemoryAdaptiveQueue<TJob>> _logger;

    private volatile ActionBlock<TJob> _actionBlock;
    private CancellationToken _cancellationToken;
    private bool _disposed;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryAdaptiveQueue{TJob}"/> class.
    /// </summary>
    /// <param name="optionsMonitor">
    /// Monitors for changes in <see cref="AdaptiveQueueOptions"/> and triggers reconfiguration of the queue.
    /// </param>
    /// <param name="logger">The logger instance for logging queue activities.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="optionsMonitor"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public InMemoryAdaptiveQueue(IOptionsMonitor<AdaptiveQueueOptions> optionsMonitor,
        ILogger<InMemoryAdaptiveQueue<TJob>> logger)
    {
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Watch for options changes and update ActionBlock.
        _optionsMonitor.OnChange(OnOptionsChanged);
    }
    
    /// <summary>
    /// Starts the adaptive queue with the specified cancellation token.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. If cancellation is requested,
    /// the queue will stop accepting new jobs and attempt to complete processing current jobs.
    /// </param>
    /// <returns>A completed <see cref="Task"/> representing the start operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        RecreateActionBlock();
        _logger.LogInformation("Queue started with concurrency={0}, capacity={1}",
            _optionsMonitor.CurrentValue.MaxDegreeOfParallelism,
            _optionsMonitor.CurrentValue.BoundedCapacity);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Signals the queue to stop accepting new jobs and completes processing of existing jobs.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. If cancellation is requested,
    /// the queue may stop processing immediately.
    /// </param>
    /// <returns>A completed <see cref="Task"/> representing the stop operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing the queue.");
        _actionBlock?.Complete();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Attempts to enqueue a job into the queue.
    /// </summary>
    /// <param name="job">The job to enqueue.</param>
    /// <returns>
    /// <c>true</c> if the job was successfully enqueued; otherwise, <c>false</c>
    /// if the queue is full or not accepting new jobs.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="job"/> is <c>null</c>.</exception>
    public bool TryEnqueue(TJob job)
    {
        if (job is null) throw new ArgumentNullException(nameof(job));

        bool success = _actionBlock.Post(job);
        _logger.LogDebug(success ? "Enqueued {JobId}" : "Queue full or not accepting: {JobId}", job.Id);
        return success;
    }

    /// <summary>
    /// Asynchronously attempts to enqueue a job into the queue.
    /// </summary>
    /// <param name="job">The job to enqueue.</param>
    /// <param name="ct">A <see cref="CancellationToken"/> to observe while waiting for the enqueue operation to complete.</param>
    /// <returns>
    /// A <see cref="Task{Boolean}"/> that represents the asynchronous enqueue operation.
    /// The task result contains <c>true</c> if the job was successfully enqueued; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="job"/> is <c>null</c>.</exception>
    public async Task<bool> TryEnqueueAsync(TJob job, CancellationToken ct)
    {
        if (job == null) throw new ArgumentNullException(nameof(job));

        bool success = await _actionBlock.SendAsync(job, ct);
        _logger.LogDebug(success ? "Enqueued {JobId}" : "Queue full or not accepting: {JobId}", job.Id);
        return success;
    }

    /// <summary>
    /// Asynchronously disposes the queue, ensuring that all enqueued jobs are processed before disposal.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous dispose operation.
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogInformation("Disposing queue...");
        await StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Handles changes to the <see cref="AdaptiveQueueOptions"/> by recreating the <see cref="ActionBlock{T}"/> with updated settings.
    /// </summary>
    /// <param name="newOptions">The new queue options.</param>
    private void OnOptionsChanged(AdaptiveQueueOptions newOptions)
    {
        _logger.LogInformation("Options changed, recreating ActionBlock...");
        RecreateActionBlock();
    }
    
    /// <summary>
    /// Recreates the internal <see cref="ActionBlock{T}"/> with the current settings from <see cref="_optionsMonitor"/>.
    /// This method ensures that the queue adapts to new concurrency and capacity settings on the fly.
    /// </summary>
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
                job.TaskCompletionSource?.SetResult(true);
            }
            catch (OperationCanceledException ex)
            {
                job.TaskCompletionSource?.SetCanceled();
                _logger.LogWarning("Job canceled {JobId}", job.Id);
            }
            catch (Exception ex)
            {
                job.TaskCompletionSource?.SetException(ex);
                _logger.LogError(ex, "Error processing {JobId}", job.Id);
            }
        }, dataflowOptions);

        // Swap old block out, if any
        var oldBlock = _actionBlock;
        _actionBlock = newBlock;
        oldBlock?.Complete();
    }
}