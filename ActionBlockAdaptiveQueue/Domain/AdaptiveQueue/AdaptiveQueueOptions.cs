namespace ActionBlockAdaptiveQueue.Domain;

/// <summary>
/// Example options class for storing concurrency/bounded capacity.
/// </summary>
public class AdaptiveQueueOptions
{
    public int MaxDegreeOfParallelism { get; set; } = 1;
    public int BoundedCapacity { get; set; } = 10000;
}