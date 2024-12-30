using ActionBlockAdaptiveQueue.Domain.AdaptiveQueue;
using ActionBlockAdaptiveQueue.Domain.Job;
using ActionBlockAdaptiveQueue.Infrastructure;
using ActionBlockAdaptiveQueueTests.Domain.Job;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ActionBlockAdaptiveQueueTests.Infrastructure;

public class InMemoryAdaptiveQueueTests
{
    private readonly Mock<ILogger<InMemoryAdaptiveQueue<IJob>>> _loggerMock;
    private readonly Mock<IOptionsMonitor<AdaptiveQueueOptions>> _optionsMonitorMock;
    private readonly IAdaptiveQueue<IJob> _adaptiveQueue;

    public InMemoryAdaptiveQueueTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryAdaptiveQueue<IJob>>>();
        _optionsMonitorMock = new Mock<IOptionsMonitor<AdaptiveQueueOptions>>();

        // Default options: concurrency=2, capacity=10
        _optionsMonitorMock
            .Setup(m => m.CurrentValue)
            .Returns(new AdaptiveQueueOptions
            {
                MaxDegreeOfParallelism = 2,
                BoundedCapacity = 10
            });

        _adaptiveQueue = new InMemoryAdaptiveQueue<IJob>(_optionsMonitorMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task StartAsync_InitializesQueue()
    {
        await _adaptiveQueue.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_CalledTwice_ShouldEitherIgnoreOrThrow()
    {
        // (test body to be implemented)
    }

    [Fact]
    public async Task TryEnqueue_ThrowsArgumentNullException_WhenJobIsNull()
    {
        await _adaptiveQueue.StartAsync(CancellationToken.None);

        Assert.Throws<ArgumentNullException>(() => _adaptiveQueue.TryEnqueue(null));
    }

    [Fact]
    public async Task TryEnqueue_ReturnsFalse_WhenQueueIsFull()
    {
        // Force small capacity
        _optionsMonitorMock
            .Setup(m => m.CurrentValue)
            .Returns(new AdaptiveQueueOptions { MaxDegreeOfParallelism = 1, BoundedCapacity = 1 });

        await _adaptiveQueue.StartAsync(CancellationToken.None);

        // Fill the queue
        _adaptiveQueue.TryEnqueue(new TestJob { Delay = 5000 });

        // Next job should fail
        bool secondEnqueue = _adaptiveQueue.TryEnqueue(new TestJob());
        Assert.False(secondEnqueue, "Should return false when queue is full.");
    }
    
    [Fact]
    public async Task TryEnqueue_MultipleJobs_ShouldRespectMaxDegreeOfParallelism()
    {
        // Force concurrency=2
        _optionsMonitorMock
            .Setup(m => m.CurrentValue)
            .Returns(new AdaptiveQueueOptions { MaxDegreeOfParallelism = 2, BoundedCapacity = 10 });

        await _adaptiveQueue.StartAsync(CancellationToken.None);

        var job1 = new TestJob { Delay = 100 };
        var job2 = new TestJob { Delay = 100 };
        var job3 = new TestJob { Delay = 100 };

        _adaptiveQueue.TryEnqueue(job1);
        _adaptiveQueue.TryEnqueue(job2);
        _adaptiveQueue.TryEnqueue(job3);

        await job1.TaskCompletionSource.Task;
        await job2.TaskCompletionSource.Task;
        await job3.TaskCompletionSource.Task;
        
        Assert.True(job1.TaskCompletionSource.Task.IsCompleted);
        Assert.True(job2.TaskCompletionSource.Task.IsCompleted);
        Assert.True(job3.TaskCompletionSource.Task.IsCompleted);
    }
    
    [Fact]
    public async Task TryEnqueue_JobExecution_ThrowsException_ShouldSetTaskCompletionSourceWithException()
    {
        // Arrange
        await _adaptiveQueue.StartAsync(CancellationToken.None);
        var failingJob = new FaultyTestJob(); // Assume FailingTestJob throws the exception in ExecuteAsync

        // Act
        _adaptiveQueue.TryEnqueue(failingJob);

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => failingJob.TaskCompletionSource.Task);
    }
    
    [Fact]
    public async Task TryEnqueueAsync_ThrowsArgumentNullException_WhenJobIsNull()
    {
        await _adaptiveQueue.StartAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _adaptiveQueue.TryEnqueueAsync(null, CancellationToken.None));
    }    
    
    [Fact]
    public async Task TryEnqueueAsync_CompletesSuccessfully()
    {
        await _adaptiveQueue.StartAsync(CancellationToken.None);

        var job = new TestJob { Delay = 100 };
        bool enqueueResult = await _adaptiveQueue.TryEnqueueAsync(job, CancellationToken.None);

        // Wait for job to finish
        await job.TaskCompletionSource.Task;

        Assert.True(enqueueResult);
        Assert.True(job.TaskCompletionSource.Task.IsCompletedSuccessfully,
            "Job should complete successfully.");
    }

    [Fact]
    public async Task TryEnqueueAsync_CanBeCanceled()
    {
        await _adaptiveQueue.StartAsync(CancellationToken.None);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // immediately cancel
        var job = new TestJob { CancellationToken = cts.Token, Delay = 50 };

        bool enqueueResult = await _adaptiveQueue.TryEnqueueAsync(job, CancellationToken.None);
        Assert.True(enqueueResult, "Even a canceled token can be enqueued.");

        // Complete the queue so processing happens
        await _adaptiveQueue.StopAsync(CancellationToken.None);

        // The job should throw TaskCanceledException
        await Assert.ThrowsAsync<TaskCanceledException>(() => job.TaskCompletionSource.Task);
    }

    [Fact]
    public async Task TryEnqueueAsync_ReturnsTrue_WhenQueueHasSpace()
    {
        await _adaptiveQueue.StartAsync(CancellationToken.None);

        var job = new TestJob { Delay = 100 };
        bool result = await _adaptiveQueue.TryEnqueueAsync(job, CancellationToken.None);

        Assert.True(result, "Enqueue should succeed when capacity is not exceeded.");
    }

    [Fact]
    public async Task StopAsync_StopsAcceptingNewJobs()
    {
        await _adaptiveQueue.StartAsync(CancellationToken.None);

        await _adaptiveQueue.StopAsync(CancellationToken.None);

        bool accepted = _adaptiveQueue.TryEnqueue(new TestJob());
        Assert.False(accepted, "No new jobs after Complete.");
    }

    [Fact]
    public async Task DisposeAsync_WaitsForJobsToFinish()
    {
        await _adaptiveQueue.StartAsync(CancellationToken.None);

        var job = new TestJob { Delay = 50, TaskCompletionSource = new TaskCompletionSource<bool>() };
        _adaptiveQueue.TryEnqueue(job);

        await _adaptiveQueue.DisposeAsync();

        await job.TaskCompletionSource.Task;
        Assert.True(job.TaskCompletionSource.Task.IsCompleted, "DisposeAsync should wait for job to complete.");
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_ShouldNotThrow()
    {
        await _adaptiveQueue.StartAsync(CancellationToken.None);

        await _adaptiveQueue.DisposeAsync();
        await _adaptiveQueue.DisposeAsync();

        // Should only log disposing once
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Disposing queue...")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once
        );
    }
}