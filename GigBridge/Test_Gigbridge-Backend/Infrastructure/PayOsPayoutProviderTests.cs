using Application.Features.Wallets.Common.Models;
using Application.Features.Wallets.Common.Interfaces;
using Infrastructure.ExternalServices.Payments;
using Microsoft.Extensions.Caching.Memory;
using PayOS;
using PayOS.Crypto;
using PayOS.Models.V1.Payouts;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Test_Gigbridge_Backend.Infrastructure;

public sealed class PayOsPayoutProviderTests
{
    [Fact]
    public void ProcessingPayoutIsNotMappedToSuccess()
    {
        var payout = new Payout
        {
            Id = "payout-1",
            ReferenceId = "wd_test",
            ApprovalState = PayoutApprovalState.Processing,
            Transactions =
            [
                new PayoutTransaction
                {
                    Id = "transaction-1",
                    ReferenceId = "wd_test",
                    State = PayoutTransactionState.Processing
                }
            ]
        };

        var result = PayOsPayoutProvider.Map(payout);

        Assert.Equal(PayoutProviderOutcome.Accepted, result.Outcome);
        Assert.Equal("payout-1", result.ProviderPayoutId);
        Assert.Equal("Processing:Processing", result.RawStatus);
    }

    [Fact]
    public void CompletedPayoutRequiresSucceededTransactions()
    {
        var payout = CreatePayout(PayoutApprovalState.Completed, PayoutTransactionState.Succeeded);

        var result = PayOsPayoutProvider.Map(payout);

        Assert.Equal(PayoutProviderOutcome.Succeeded, result.Outcome);
    }

    [Fact]
    public void RejectedPayoutMapsToFailed()
    {
        var payout = CreatePayout(PayoutApprovalState.Rejected, PayoutTransactionState.Failed);

        var result = PayOsPayoutProvider.Map(payout);

        Assert.Equal(PayoutProviderOutcome.Failed, result.Outcome);
    }

    [Fact]
    public void AmbiguousCompletedPayoutRequiresSync()
    {
        var payout = CreatePayout(PayoutApprovalState.Completed, PayoutTransactionState.Processing);

        var result = PayOsPayoutProvider.Map(payout);

        Assert.Equal(PayoutProviderOutcome.SyncRequired, result.Outcome);
    }

    [Fact]
    public async Task AvailabilityReturnsVndBalanceAndCachesResponse()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                """
                {"code":"00","desc":"success","data":{"accountNumber":"masked","accountName":"GigBridge","currency":"VND","balance":"150000"}}
                """)));
        var provider = CreateProvider(handler);

        var first = await provider.CheckAvailabilityAsync(CancellationToken.None);
        var cached = await provider.CheckAvailabilityAsync(CancellationToken.None);

        Assert.True(first.IsAvailable);
        Assert.Equal(150_000m, first.BalanceVnd);
        Assert.Equal(first, cached);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task AvailabilityMapsForbiddenWithoutLeakingResponse()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            Task.FromResult(JsonResponse(
                HttpStatusCode.Forbidden,
                """{"code":"403","desc":"address is not allowed"}""")));
        var provider = CreateProvider(handler);

        var result = await provider.CheckAvailabilityAsync(CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Equal("HTTP_403", result.ErrorCode);
        Assert.Contains("whitelist", result.SafeMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("address is not allowed", result.SafeMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AvailabilityMapsUnauthorizedCredentials()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            Task.FromResult(JsonResponse(
                HttpStatusCode.Unauthorized,
                """{"code":"401","desc":"invalid api key"}""")));
        var provider = CreateProvider(handler);

        var result = await provider.CheckAvailabilityAsync(CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Equal("HTTP_401", result.ErrorCode);
        Assert.Contains("credentials", result.SafeMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invalid api key", result.SafeMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AvailabilityHidesProxyDetailsOnConnectionFailure()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            Task.FromException<HttpResponseMessage>(
                new HttpRequestException("http://user:secret@proxy.test:8080")));
        var provider = CreateProvider(handler);

        var result = await provider.CheckAvailabilityAsync(CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Equal("NETWORK_ERROR", result.ErrorCode);
        Assert.DoesNotContain("proxy.test", result.SafeMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.SafeMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AvailabilityMapsTimeout()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("timed out")));
        var provider = CreateProvider(handler);

        var result = await provider.CheckAvailabilityAsync(CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Equal("TIMEOUT", result.ErrorCode);
        Assert.Contains("timed out", result.SafeMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PayoutDirectHandlerUsesIpv4WithoutSystemProxy()
    {
        using var handler = global::Infrastructure.DependencyInjection.CreatePayoutDirectHandler();

        Assert.False(handler.UseProxy);
        Assert.NotNull(handler.ConnectCallback);
    }

    [Fact]
    public void PayoutProxyHandlerUsesConfiguredProxyCredentials()
    {
        using var handler = global::Infrastructure.DependencyInjection.CreatePayoutProxyHandler(
            "http://proxy-user:proxy-pass@proxy.test:8080");

        var proxy = Assert.IsType<WebProxy>(handler.Proxy);
        var credential = proxy.Credentials!.GetCredential(proxy.Address!, "Basic");
        Assert.True(handler.UseProxy);
        Assert.Equal("proxy.test", proxy.Address!.Host);
        Assert.Equal("proxy-user", credential!.UserName);
        Assert.Equal("proxy-pass", credential.Password);
    }

    [Fact]
    public void PayoutProxyHandlerRejectsInvalidUrl()
    {
        Assert.Throws<UriFormatException>(() =>
            global::Infrastructure.DependencyInjection.CreatePayoutProxyHandler("not-a-proxy-url"));
    }

    [Fact]
    public async Task CreatePayoutAfterForbiddenRecoveryKeepsIdentityAndAvoidsDuplicate()
    {
        var blocked = true;
        var payoutExists = false;
        var postCount = 0;
        string? sentIdempotencyKey = null;
        string? sentReferenceId = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                if (blocked)
                {
                    return JsonResponse(HttpStatusCode.Forbidden, """{"code":"403","desc":"denied"}""");
                }

                return SignedPayoutListResponse(
                    payoutExists ? PayoutListWithExistingJson : EmptyPayoutListJson);
            }

            postCount++;
            sentIdempotencyKey = request.Headers.TryGetValues("x-idempotency-key", out var values)
                ? values.Single()
                : null;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            sentReferenceId = body.RootElement.TryGetProperty("referenceId", out var referenceId)
                ? referenceId.GetString()
                : null;
            payoutExists = true;
            return JsonResponse(
                HttpStatusCode.InternalServerError,
                """{"code":"500","desc":"ambiguous create response"}""");
        });
        var provider = CreateProvider(handler);
        var request = new PayoutCreateRequest(
            Guid.NewGuid(),
            "wd_stable_reference",
            10_000m,
            "970436",
            "123456789",
            "NGUYEN VAN A",
            "GigBridge withdrawal",
            "stable-idempotency-key");

        var forbidden = await provider.CreatePayoutAsync(request, CancellationToken.None);
        blocked = false;
        var created = await provider.CreatePayoutAsync(request, CancellationToken.None);
        var duplicate = await provider.CreatePayoutAsync(request, CancellationToken.None);

        Assert.Equal(PayoutProviderOutcome.SyncRequired, forbidden.Outcome);
        Assert.Equal(PayoutProviderOutcome.SyncRequired, created.Outcome);
        Assert.True(
            duplicate.Outcome == PayoutProviderOutcome.Accepted,
            $"{duplicate.RawStatus}: {duplicate.FailureReason}");
        Assert.Equal(1, postCount);
        Assert.Equal("stable-idempotency-key", sentIdempotencyKey);
        Assert.Equal("wd_stable_reference", sentReferenceId);
    }

    private static Payout CreatePayout(
        PayoutApprovalState approvalState,
        PayoutTransactionState transactionState)
    {
        return new Payout
        {
            Id = "payout-1",
            ReferenceId = "wd_test",
            ApprovalState = approvalState,
            Transactions =
            [
                new PayoutTransaction
                {
                    Id = "transaction-1",
                    ReferenceId = "wd_test",
                    State = transactionState
                }
            ]
        };
    }

    private static PayOsPayoutProvider CreateProvider(HttpMessageHandler handler)
    {
        var client = new PayOSClient(new PayOSOptions
        {
            ClientId = "client-id",
            ApiKey = "api-key",
            ChecksumKey = TestChecksumKey,
            BaseUrl = "https://api-merchant.payos.vn",
            HttpClient = new HttpClient(handler),
            MaxRetries = 0,
            TimeoutMs = 1_000
        });
        return new PayOsPayoutProvider(client, new MemoryCache(new MemoryCacheOptions()));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string content) =>
        new(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage SignedPayoutListResponse(string content)
    {
        var response = JsonResponse(HttpStatusCode.OK, content);
        using var body = JsonDocument.Parse(content);
        var payoutList = JsonSerializer.Deserialize<PayoutListResponse>(
            body.RootElement.GetProperty("data").GetRawText())!;
        var signature = new CryptoProvider().CreateSignature(
            TestChecksumKey,
            payoutList);
        response.Headers.Add("x-signature", signature);
        return response;
    }

    private const string TestChecksumKey = "checksum-key";

    private const string EmptyPayoutListJson =
        """{"code":"00","desc":"success","data":{"payouts":[],"pagination":{"total":0,"limit":10,"offset":0,"count":0,"hasMore":false}}}""";

    private const string PayoutListWithExistingJson =
        """{"code":"00","desc":"success","data":{"payouts":[{"id":"payout-1","referenceId":"wd_stable_reference","transactions":[{"id":"transaction-1","referenceId":"wd_stable_reference","amount":10000,"description":"GigBridge withdrawal","toBin":"970436","toAccountNumber":"123456789","toAccountName":"NGUYEN VAN A","state":"PROCESSING"}],"category":[],"approvalState":"PROCESSING","createdAt":"2026-07-18T00:00:00Z"}],"pagination":{"total":1,"limit":10,"offset":0,"count":1,"hasMore":false}}}""";

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return _send(request, cancellationToken);
        }
    }
}
