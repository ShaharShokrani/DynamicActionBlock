using ActionBlockAdaptiveQueue.Domain.Job;

namespace ActionBlockAdaptiveQueueTests.Domain.Job;

public class FaultyTestJob : IJob
{
    public Guid Id { get; }
    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        throw new InvalidOperationException();
    }

    public CancellationToken CancellationToken { get; set; }
    public TaskCompletionSource<bool> TaskCompletionSource { get; set; } = new();
}