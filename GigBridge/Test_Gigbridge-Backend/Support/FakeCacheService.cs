using Application.Common.Interfaces.IService;
using System.Collections.Concurrent;

namespace Test_Gigbridge_Backend.Support;

public sealed class FakeCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, object?> _cache = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cache.TryGetValue(key, out var value) ? (T?)value : default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        _cache[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
