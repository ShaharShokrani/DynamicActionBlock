using ActionBlockAdaptiveQueue.Domain;

namespace ActionBlockAdaptiveQueueTests.Domain.Job;

public class TestJob : IJob
{
    public int Delay;


    public Guid Id { get; }
    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public CancellationToken CancellationToken { get; set; }
    public TaskCompletionSource<bool> TaskCompletionSource { get; set; }
}