using System.Text;
using System.Text.Json;
using Infrastructure.Adapters.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using NSubstitute;

namespace Test_Gigbridge_Backend.Infrastructure.Adapters.Caching;

public sealed class HybridCacheServiceTests
{
    private const string OtpKey = "auth:otp:signup:challenge:email-hash";

    [Fact]
    public async Task SetAsync_UsesAbsoluteExpirationForBothCacheLevels()
    {
        var distributedCache = Substitute.For<IDistributedCache>();
        var memoryCache = new RecordingMemoryCache();
        DistributedCacheEntryOptions? distributedOptions = null;
        distributedCache
            .SetAsync(
                "general:key",
                Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                distributedOptions = call.ArgAt<DistributedCacheEntryOptions>(2);
                return Task.CompletedTask;
            });
        var cache = CreateCache(memoryCache, distributedCache, "Production");
        var expiration = TimeSpan.FromMinutes(12);

        await cache.SetAsync("general:key", "value", expiration);

        Assert.NotNull(distributedOptions);
        Assert.Equal(
            expiration,
            distributedOptions.AbsoluteExpirationRelativeToNow);
        Assert.Null(distributedOptions.SlidingExpiration);
        Assert.Equal(expiration, memoryCache.LastAbsoluteExpirationRelativeToNow);
        Assert.Null(memoryCache.LastSlidingExpiration);
    }

    [Fact]
    public async Task GetAsync_DoesNotPromoteDistributedHitIntoMemory()
    {
        const string key = "general:distributed-only";
        var distributedCache = Substitute.For<IDistributedCache>();
        var memoryCache = new RecordingMemoryCache();
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize("redis-value"));
        distributedCache
            .GetAsync(key, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(payload));
        var cache = CreateCache(memoryCache, distributedCache, "Production");

        var first = await cache.GetAsync<string>(key);
        var second = await cache.GetAsync<string>(key);

        Assert.Equal("redis-value", first);
        Assert.Equal("redis-value", second);
        Assert.False(memoryCache.TryGetValue(key, out _));
        await distributedCache
            .Received(2)
            .GetAsync(key, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Local")]
    [InlineData("Testing")]
    public async Task GetAsync_OtpInLocalEnvironmentsKeepsMemoryFallback(
        string environmentName)
    {
        var distributedCache = Substitute.For<IDistributedCache>();
        var memoryCache = new RecordingMemoryCache();
        memoryCache.Set(OtpKey, "local-value", TimeSpan.FromMinutes(1));
        var cache = CreateCache(
            memoryCache,
            distributedCache,
            environmentName);

        var value = await cache.GetAsync<string>(OtpKey);

        Assert.Equal("local-value", value);
        await distributedCache
            .DidNotReceive()
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_OtpInProductionFailsClosedAndBypassesMemory()
    {
        var distributedCache = Substitute.For<IDistributedCache>();
        var memoryCache = new RecordingMemoryCache();
        memoryCache.Set(OtpKey, "stale-value", TimeSpan.FromMinutes(1));
        var failure = new IOException("redis get failed");
        distributedCache
            .GetAsync(OtpKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<byte[]?>(failure));
        var cache = CreateCache(memoryCache, distributedCache, "Production");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.GetAsync<string>(OtpKey));

        Assert.Same(failure, exception.InnerException);
        await distributedCache
            .Received(1)
            .GetAsync(OtpKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_OtpInProductionFailsClosedWithoutMemoryFallback()
    {
        var distributedCache = Substitute.For<IDistributedCache>();
        var memoryCache = new RecordingMemoryCache();
        var failure = new IOException("redis set failed");
        distributedCache
            .SetAsync(
                OtpKey,
                Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(failure));
        var cache = CreateCache(memoryCache, distributedCache, "Production");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.SetAsync(
                OtpKey,
                "otp-value",
                TimeSpan.FromMinutes(5)));

        Assert.Same(failure, exception.InnerException);
        Assert.False(memoryCache.TryGetValue(OtpKey, out _));
    }

    [Fact]
    public async Task RemoveAsync_OtpInProductionFailsClosedBeforeMemoryMutation()
    {
        var distributedCache = Substitute.For<IDistributedCache>();
        var memoryCache = new RecordingMemoryCache();
        memoryCache.Set(OtpKey, "stale-value", TimeSpan.FromMinutes(1));
        var failure = new IOException("redis remove failed");
        distributedCache
            .RemoveAsync(OtpKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(failure));
        var cache = CreateCache(memoryCache, distributedCache, "Production");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.RemoveAsync(OtpKey));

        Assert.Same(failure, exception.InnerException);
        Assert.True(memoryCache.TryGetValue(OtpKey, out _));
    }

    [Fact]
    public async Task GetAndRemoveAsync_OtpDoesNotReturnValueWhenRemoveFails()
    {
        var distributedCache = Substitute.For<IDistributedCache>();
        var memoryCache = new RecordingMemoryCache();
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize("otp-value"));
        var failure = new IOException("redis remove failed");
        distributedCache
            .GetAsync(OtpKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(payload));
        distributedCache
            .RemoveAsync(OtpKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(failure));
        var cache = CreateCache(memoryCache, distributedCache, "Production");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.GetAndRemoveAsync<string>(OtpKey));

        Assert.Same(failure, exception.InnerException);
        await distributedCache
            .Received(1)
            .RemoveAsync(OtpKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_GeneralKeyKeepsMemoryFallbackWhenRedisFails()
    {
        const string key = "general:fallback";
        var distributedCache = Substitute.For<IDistributedCache>();
        var memoryCache = new RecordingMemoryCache();
        distributedCache
            .SetAsync(
                key,
                Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("redis set failed")));
        var cache = CreateCache(memoryCache, distributedCache, "Production");

        await cache.SetAsync(key, "memory-value", TimeSpan.FromMinutes(2));
        var value = await cache.GetAsync<string>(key);

        Assert.Equal("memory-value", value);
    }

    private static HybridCacheService CreateCache(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return new HybridCacheService(
            memoryCache,
            distributedCache,
            NullLogger<HybridCacheService>.Instance,
            environment);
    }

    private sealed class RecordingMemoryCache : IMemoryCache
    {
        private readonly Dictionary<object, object?> _values = [];

        public TimeSpan? LastAbsoluteExpirationRelativeToNow { get; private set; }
        public TimeSpan? LastSlidingExpiration { get; private set; }

        public ICacheEntry CreateEntry(object key) =>
            new RecordingCacheEntry(key, this);

        public void Remove(object key)
        {
            _values.Remove(key);
        }

        public bool TryGetValue(object key, out object? value) =>
            _values.TryGetValue(key, out value);

        public void Dispose()
        {
        }

        private void Commit(RecordingCacheEntry entry)
        {
            _values[entry.Key] = entry.Value;
            LastAbsoluteExpirationRelativeToNow =
                entry.AbsoluteExpirationRelativeToNow;
            LastSlidingExpiration = entry.SlidingExpiration;
        }

        private sealed class RecordingCacheEntry(
            object key,
            RecordingMemoryCache owner) : ICacheEntry
        {
            public object Key { get; } = key;
            public object? Value { get; set; }
            public DateTimeOffset? AbsoluteExpiration { get; set; }
            public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
            public TimeSpan? SlidingExpiration { get; set; }
            public IList<IChangeToken> ExpirationTokens { get; } =
                new List<IChangeToken>();
            public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } =
                new List<PostEvictionCallbackRegistration>();
            public CacheItemPriority Priority { get; set; }
            public long? Size { get; set; }

            public void Dispose()
            {
                owner.Commit(this);
            }
        }
    }
}
