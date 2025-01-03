using ActionBlockAdaptiveQueue.Domain.Job;

namespace ActionBlockAdaptiveQueueTests.Domain.Job;

public class CancelledTestJob : IJob
{
    public Guid Id { get; }
    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public CancellationToken CancellationToken { get; set; }
    public TaskCompletionSource<bool> TaskCompletionSource { get; set; }
}