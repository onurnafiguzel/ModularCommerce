using System.Collections.Concurrent;
using ModularCommerce.Catalog.Infrastructure.Caching;

namespace ModularCommerce.Catalog.UnitTests.Caching;

/// <summary>Decorator testleri için bellek-içi IProductCache (Redis'siz, deterministik).</summary>
internal sealed class FakeProductCache : IProductCache
{
    private readonly ConcurrentDictionary<string, object> _store = new();

    public int Writes { get; private set; }

    public void Seed<T>(string key, T value) where T : class => _store[key] = value;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
        => Task.FromResult(_store.TryGetValue(key, out var value) ? (T?)value : null);

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class
    {
        Writes++;
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
