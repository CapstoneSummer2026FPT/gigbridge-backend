using System.Text.Json;
using Application.Common.Interfaces.IService;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Caching;

public class HybridCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<HybridCacheService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HybridCacheService(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ILogger<HybridCacheService> logger)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        // Fast path: check the in-memory cache first
        if (_memoryCache.TryGetValue(key, out T? memoryValue))
        {
            return memoryValue;
        }

        // Slow path: try the distributed cache (Redis)
        try
        {
            var distributedValue = await _distributedCache.GetStringAsync(key, cancellationToken);
            if (distributedValue != null)
            {
                var value = JsonSerializer.Deserialize<T>(distributedValue, JsonOptions);
                if (value != null)
                {
                    // Promote to local memory for subsequent fast access
                    _memoryCache.Set(key, value, TimeSpan.FromMinutes(5));
                }
                return value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache read failed for key {CacheKey}. Falling through to data source.", key);
        }

        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, CancellationToken cancellationToken = default)
    {
        var expiration = slidingExpiration ?? TimeSpan.FromHours(1);

        // Always populate local memory
        _memoryCache.Set(key, value, expiration);

        // Best-effort distributed set
        try
        {
            var cacheOptions = new DistributedCacheEntryOptions
            {
                SlidingExpiration = expiration
            };
            var serializedValue = JsonSerializer.Serialize(value);
            await _distributedCache.SetStringAsync(key, serializedValue, cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache write failed for key {CacheKey}. Local cache still populated.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);

        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache remove failed for key {CacheKey}. Local entry already removed.", key);
        }
    }
}