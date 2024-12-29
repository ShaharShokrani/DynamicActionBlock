using ActionBlockAdaptiveQueue.Domain;

namespace ActionBlockAdaptiveQueueTests.Domain.Job;

public class FaultyTestJob : IJob
{
    public Guid Id { get; }
    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public CancellationToken CancellationToken { get; set; }
    public TaskCompletionSource<bool> TaskCompletionSource { get; set; }
}