using ActionBlockAdaptiveQueue.Domain.Job;

namespace ActionBlockAdaptiveQueueTests.Domain.Job;

public class TestJob : IJob
{
    public int Delay;


    public Guid Id { get; }
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Delay, cancellationToken);
    }

    public CancellationToken CancellationToken { get; set; }
    public TaskCompletionSource<bool> TaskCompletionSource { get; set; } = new();
}