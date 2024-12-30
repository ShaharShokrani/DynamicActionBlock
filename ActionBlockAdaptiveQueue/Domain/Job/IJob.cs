namespace ActionBlockAdaptiveQueue.Domain.Job;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IJob
{
    public Guid Id { get; }
    
    /// <summary>
    /// Executes the job with the provided parameters and cancellation token.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the job.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ExecuteAsync(CancellationToken cancellationToken);

    public CancellationToken CancellationToken { get; set; }
    
    /// <summary>
    /// TaskCompletionSource to signal job completion.
    /// </summary>
    public TaskCompletionSource<bool> TaskCompletionSource { get; set; }
}