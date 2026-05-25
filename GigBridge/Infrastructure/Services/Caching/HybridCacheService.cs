using System.Text.Json;
using Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
namespace Infrastructure.Services.Caching;
public class HybridCacheService : ICacheService {
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    public HybridCacheService(IMemoryCache memoryCache, IDistributedCache distributedCache) {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
    }
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) {
        if (_memoryCache.TryGetValue(key, out T? memoryValue)) {
            return memoryValue;
        }
        var distributedValue = await _distributedCache.GetStringAsync(key, cancellationToken);
        if (distributedValue != null) {
            var value = JsonSerializer.Deserialize<T>(distributedValue);
            if (value != null) {
                _memoryCache.Set(key, value, TimeSpan.FromMinutes(5));
            }
            return value;
        }
        return default;
    }
    public async Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default) {
        var cacheOptions = new DistributedCacheEntryOptions {
            SlidingExpiration = slidingExpiration ?? TimeSpan.FromHours(1)
        };
        var serializedValue = JsonSerializer.Serialize(value);
        await _distributedCache.SetStringAsync(key, serializedValue, cacheOptions, cancellationToken);
        _memoryCache.Set(key, value, slidingExpiration ?? TimeSpan.FromHours(1));
    }
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default) {
        _memoryCache.Remove(key);
        await _distributedCache.RemoveAsync(key, cancellationToken);
    }
}