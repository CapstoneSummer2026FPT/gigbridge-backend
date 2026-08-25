using Application.Common.Exceptions;
using Application.Common.Interfaces.Caching;
using Application.Common.InternalServices.Wallets.Models;
using Infrastructure.ExternalServices.Banking.VietQr;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace Test_Gigbridge_Backend.Infrastructure.ExternalServices.Banking.VietQr;

public sealed class VietQrBankDirectoryTests
{
    private const string FreshCacheKey = "wallet:supported-banks:vietqr:fresh:v1";
    private const string StaleCacheKey = "wallet:supported-banks:vietqr:stale:v1";

    [Fact]
    public async Task GetBanksAsync_NormalizesFiltersDeduplicatesAndCachesResponse()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "code":"00",
              "data":[
                {"bin":"970436","code":"vcb","shortName":"Vietcombank","name":"Ngân hàng Vietcombank","logo":"https://api.vietqr.io/img/VCB.png"},
                {"bin":"970436","code":"ZZZ","shortName":"Duplicate","name":"Duplicate bank","logo":null},
                {"bin":"970416","code":"ACB","shortName":"ACB","name":"Ngân hàng Á Châu","logo":"http://api.vietqr.io/img/ACB.png"},
                {"bin":"invalid","code":"BAD","shortName":"Bad","name":"Invalid bank","logo":null}
              ]
            }
            """)));
        var cache = new RecordingCacheService();
        var directory = CreateDirectory(handler, cache);

        var banks = await directory.GetBanksAsync(CancellationToken.None);

        Assert.Equal(2, banks.Count);
        Assert.Equal("ACB", banks[0].Code);
        Assert.Null(banks[0].Logo);
        Assert.Equal("VCB", banks[1].Code);
        Assert.Equal("https://api.vietqr.io/img/VCB.png", banks[1].Logo);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(TimeSpan.FromHours(24), cache.Expirations[FreshCacheKey]);
        Assert.Equal(TimeSpan.FromDays(7), cache.Expirations[StaleCacheKey]);
    }

    [Fact]
    public async Task GetBanksAsync_UsesFreshCacheWithoutCallingVietQr()
    {
        var cached = Banks();
        var cache = new RecordingCacheService();
        await cache.SetAsync(FreshCacheKey, cached, TimeSpan.FromHours(24));
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("HTTP must not be called for a fresh cache hit."));
        var directory = CreateDirectory(handler, cache);

        var result = await directory.GetBanksAsync(CancellationToken.None);

        Assert.Equal(cached, result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetBanksAsync_UsesStaleCacheWhenRefreshFails()
    {
        var stale = Banks();
        var cache = new RecordingCacheService();
        await cache.SetAsync(StaleCacheKey, stale, TimeSpan.FromDays(7));
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var directory = CreateDirectory(handler, cache);

        var result = await directory.GetBanksAsync(CancellationToken.None);

        Assert.Equal(stale, result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetBanksAsync_UsesStaleCacheForProviderTimeout()
    {
        var stale = Banks();
        var cache = new RecordingCacheService();
        await cache.SetAsync(StaleCacheKey, stale, TimeSpan.FromDays(7));
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("Provider timeout.")));
        var directory = CreateDirectory(handler, cache);

        var result = await directory.GetBanksAsync(CancellationToken.None);

        Assert.Equal(stale, result);
    }

    [Fact]
    public async Task GetBanksAsync_UsesStaleCacheForMalformedJson()
    {
        var stale = Banks();
        var cache = new RecordingCacheService();
        await cache.SetAsync(StaleCacheKey, stale, TimeSpan.FromDays(7));
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            "not-json")));
        var directory = CreateDirectory(handler, cache);

        var result = await directory.GetBanksAsync(CancellationToken.None);

        Assert.Equal(stale, result);
    }

    [Fact]
    public async Task GetBanksAsync_ThrowsServiceUnavailableWhenNoCacheExists()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            """{"code":"00","data":[]}""")));
        var directory = CreateDirectory(handler, new RecordingCacheService());

        var exception = await Assert.ThrowsAsync<ExternalServiceException>(() =>
            directory.GetBanksAsync(CancellationToken.None));

        Assert.Contains("temporarily unavailable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetBanksAsync_PropagatesCallerCancellationWithoutUsingStaleCache()
    {
        var stale = Banks();
        var cache = new RecordingCacheService();
        await cache.SetAsync(StaleCacheKey, stale, TimeSpan.FromDays(7));
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHttpMessageHandler((_, token) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(token);
        });
        var directory = CreateDirectory(handler, cache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            directory.GetBanksAsync(cancellation.Token));
    }

    private static VietQrBankDirectory CreateDirectory(
        HttpMessageHandler handler,
        ICacheService cache)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.vietqr.io/")
        };
        return new VietQrBankDirectory(
            client,
            cache,
            NullLogger<VietQrBankDirectory>.Instance);
    }

    private static SupportedBank[] Banks() =>
        [new SupportedBank("970436", "VCB", "Vietcombank", "Ngân hàng Vietcombank", null)];

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string content) =>
        new(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return callback(request, cancellationToken);
        }
    }

    private sealed class RecordingCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

        public Dictionary<string, TimeSpan?> Expirations { get; } = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                _values.TryGetValue(key, out var value) && value is T typed ? typed : default);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiration = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values[key] = value!;
            Expirations[key] = expiration;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
