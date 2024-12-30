using ActionBlockAdaptiveQueue.Domain.Job;

namespace ActionBlockAdaptiveQueue.Domain.AdaptiveQueue;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Represents a dynamic concurrency job queue that supports
/// starting, stopping, and enqueuing of jobs.
/// </summary>
/// <typeparam name="TJob">
/// The type of job to be processed.
/// Must implement <see cref="IJob"/>.
/// </typeparam>
public interface IAdaptiveQueue<in TJob> : IAsyncDisposable
    where TJob : IJob
{
    /// <summary>
    /// Tries to enqueue a job into the queue.
    /// </summary>
    /// <param name="job">The job to enqueue.</param>
    /// <returns>
    /// <c>true</c> if the job was successfully enqueued;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="job"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the queue is not accepting new jobs.
    /// </exception>
    bool TryEnqueue(TJob job);

    /// <summary>
    /// Asynchronously attempts to enqueue a job into the queue.
    /// </summary>
    /// <param name="job">The job to enqueue.</param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the operation.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> whose result is <c>true</c>
    /// if the job was successfully enqueued; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="job"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the queue is not accepting new jobs.
    /// </exception>
    Task<bool> TryEnqueueAsync(TJob job, CancellationToken cancellationToken);

    /// <summary>
    /// Initializes and starts the job queue.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to observe while waiting for the operation to complete.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous start operation.
    /// </returns>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Signals the queue to stop accepting new jobs and
    /// initiates a graceful shutdown of all processing.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to observe while waiting for the operation to complete.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous stop operation.
    /// </returns>
    Task StopAsync(CancellationToken cancellationToken);
}