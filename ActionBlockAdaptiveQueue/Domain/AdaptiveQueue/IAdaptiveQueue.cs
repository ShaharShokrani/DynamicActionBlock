namespace ActionBlockAdaptiveQueue.Domain.IAdaptiveQueue;

using System.Threading;
using System.Threading.Tasks;

public interface IAdaptiveQueue<in TJob> : IAsyncDisposable where TJob : IJob
{
    /// <summary>
    /// Tries to enqueue a job into the queue.
    /// </summary>
    /// <param name="job">The job to enqueue.</param>
    /// <returns>True if the job was successfully enqueued; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the job is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the queue is not accepting new jobs.</exception>
    bool TryEnqueue(TJob job);

    /// <summary>
    /// Tries to enqueue a job into the queue.
    /// </summary>
    /// <param name="job">The job to enqueue.</param>
    /// <returns>True if the job was successfully enqueued; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the job is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the queue is not accepting new jobs.</exception>
    Task<bool> TryEnqueueAsync(TJob job, CancellationToken cancellationToken);
    
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}