using System.Collections.Concurrent;
using System.Text.Json;
using Application.Common.Interfaces.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Caching;

public class HybridCacheService : ICacheService
{
    private const string OtpKeyPrefix = "auth:otp:";
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(1);

    private sealed class CacheKeyLock
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int References { get; set; }
    }

    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<HybridCacheService> _logger;
    private readonly bool _failClosedForOtp;
    private readonly ConcurrentDictionary<string, CacheKeyLock> _keyLocks = new();
    private readonly object _keyLocksSync = new();

    public HybridCacheService(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ILogger<HybridCacheService> logger,
        IHostEnvironment environment)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _logger = logger;
        _failClosedForOtp = !environment.IsDevelopment()
            && !environment.IsEnvironment("Local")
            && !environment.IsEnvironment("Testing");
    }

    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
    {
        var requiresDistributedCache = RequiresDistributedCache(key);
        if (!requiresDistributedCache
            && _memoryCache.TryGetValue(key, out T? memoryValue))
        {
            return memoryValue;
        }

        try
        {
            var distributedValue = await _distributedCache.GetStringAsync(
                key,
                cancellationToken);
            return distributedValue is null
                ? default
                : JsonSerializer.Deserialize<T>(distributedValue);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (requiresDistributedCache)
            {
                _logger.LogError(
                    "Distributed cache GET failed for security-sensitive OTP state");
                throw CreateOtpCacheUnavailableException(ex);
            }

            _logger.LogWarning(
                ex,
                "Redis unavailable for GET; falling back to memory cache");
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveExpiration = expiration ?? DefaultExpiration;
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = effectiveExpiration
        };
        var serializedValue = JsonSerializer.Serialize(value);
        var requiresDistributedCache = RequiresDistributedCache(key);

        try
        {
            await _distributedCache.SetStringAsync(
                key,
                serializedValue,
                cacheOptions,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (requiresDistributedCache)
            {
                _logger.LogError(
                    "Distributed cache SET failed for security-sensitive OTP state");
                throw CreateOtpCacheUnavailableException(ex);
            }

            _logger.LogWarning(
                ex,
                "Redis unavailable for SET; keeping value in memory cache only");
        }

        if (!requiresDistributedCache)
        {
            _memoryCache.Set(
                key,
                value,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = effectiveExpiration
                });
        }
    }

    public async Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var requiresDistributedCache = RequiresDistributedCache(key);
        if (!requiresDistributedCache)
        {
            _memoryCache.Remove(key);
        }

        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (requiresDistributedCache)
            {
                _logger.LogError(
                    "Distributed cache REMOVE failed for security-sensitive OTP state");
                throw CreateOtpCacheUnavailableException(ex);
            }

            _logger.LogWarning(ex, "Redis unavailable for REMOVE");
            return;
        }

        if (requiresDistributedCache)
        {
            _memoryCache.Remove(key);
        }
    }

    public async Task<T?> GetAndRemoveAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
    {
        CacheKeyLock keyLock;
        lock (_keyLocksSync)
        {
            keyLock = _keyLocks.GetOrAdd(key, _ => new CacheKeyLock());
            keyLock.References++;
        }

        var lockAcquired = false;
        try
        {
            await keyLock.Gate.WaitAsync(cancellationToken);
            lockAcquired = true;
            var value = await GetAsync<T>(key, cancellationToken);
            if (value is not null)
            {
                await RemoveAsync(key, cancellationToken);
            }

            return value;
        }
        finally
        {
            if (lockAcquired)
            {
                keyLock.Gate.Release();
            }

            lock (_keyLocksSync)
            {
                keyLock.References--;
                if (keyLock.References == 0)
                {
                    _keyLocks.TryRemove(
                        new KeyValuePair<string, CacheKeyLock>(key, keyLock));
                    keyLock.Gate.Dispose();
                }
            }
        }
    }

    private bool RequiresDistributedCache(string key) =>
        _failClosedForOtp
        && key.StartsWith(OtpKeyPrefix, StringComparison.Ordinal);

    private static InvalidOperationException CreateOtpCacheUnavailableException(
        Exception innerException) =>
        new(
            "The security-sensitive distributed cache is unavailable.",
            innerException);
}
