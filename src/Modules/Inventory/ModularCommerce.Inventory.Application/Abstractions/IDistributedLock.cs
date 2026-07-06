namespace ModularCommerce.Inventory.Application.Abstractions;

public interface IDistributedLock
{
    Task<ILockHandle?> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        TimeSpan waitBudget,
        CancellationToken cancellationToken);
}

public interface ILockHandle : IAsyncDisposable
{
    TimeSpan WaitedFor { get; }
}
