using Application.Features.Wallets.Common.Models;
using Application.Features.Wallets.Common.Interfaces;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models;
using PayOS.Models.V1.Payouts;

namespace Infrastructure.ExternalServices.Payments;

public sealed class PayOsPayoutProvider : IPayoutProvider
{
    private const string AvailabilityCacheKey = "payos:payout:availability";
    private readonly PayOSClient _client;
    private readonly IMemoryCache _cache;

    public PayOsPayoutProvider(PayOSClient client, IMemoryCache cache)
    {
        _client = client;
        _cache = cache;
    }

    public string ProviderName => "PayOS";

    public async Task<PayoutProviderResult> CreatePayoutAsync(
        PayoutCreateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var existing = await FindByReferenceIdAsync(request.ProviderOrderCode, cancellationToken);
            if (existing is not null)
            {
                return Map(existing);
            }

            var payout = await _client.Payouts.CreateAsync(
                new PayoutRequest
                {
                    ReferenceId = request.ProviderOrderCode,
                    Amount = checked(Convert.ToInt64(request.AmountVnd)),
                    Description = request.Description,
                    ToBin = request.BankBin,
                    ToAccountNumber = request.AccountNumber
                },
                request.IdempotencyKey,
                new RequestOptions<Payout>
                {
                    CancellationToken = cancellationToken,
                    MaxRetries = 0
                });

            return Map(payout);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SyncRequired("PayOS payout request timed out.", "TIMEOUT");
        }
        catch (UserAbortException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return MapException("payout request", ex);
        }
    }

    public async Task<PayoutProviderResult> GetPayoutStatusAsync(
        PayoutStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var payout = string.IsNullOrWhiteSpace(request.ProviderPayoutId)
                ? await FindByReferenceIdAsync(request.ProviderOrderCode, cancellationToken)
                : await _client.Payouts.GetAsync(
                    request.ProviderPayoutId,
                    new RequestOptions { CancellationToken = cancellationToken, MaxRetries = 0 });

            return payout is null
                ? SyncRequired("PayOS payout was not found by reference ID.")
                : Map(payout);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SyncRequired("PayOS payout status request timed out.", "TIMEOUT");
        }
        catch (UserAbortException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return MapException("payout status request", ex);
        }
    }

    public async Task<PayoutProviderAvailability> CheckAvailabilityAsync(
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<PayoutProviderAvailability>(AvailabilityCacheKey, out var cached))
        {
            return cached!;
        }

        PayoutProviderAvailability result;
        try
        {
            var account = await _client.PayoutsAccount.GetBalanceAsync(
                new RequestOptions
                {
                    CancellationToken = cancellationToken,
                    MaxRetries = 0
                });
            if (!string.Equals(account.Currency, "VND", StringComparison.OrdinalIgnoreCase) ||
                !decimal.TryParse(
                    account.Balance,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var balance))
            {
                result = new PayoutProviderAvailability(
                    false,
                    null,
                    "INVALID_BALANCE_RESPONSE",
                    "PayOS payout account returned an invalid balance response.");
            }
            else
            {
                result = new PayoutProviderAvailability(true, balance, null, null);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = new PayoutProviderAvailability(
                false,
                null,
                "TIMEOUT",
                "PayOS payout availability check timed out.");
        }
        catch (UserAbortException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var mapped = MapException("payout availability check", ex);
            result = new PayoutProviderAvailability(
                false,
                null,
                mapped.RawStatus,
                mapped.FailureReason);
        }

        _cache.Set(AvailabilityCacheKey, result, TimeSpan.FromSeconds(30));
        return result;
    }

    private async Task<Payout?> FindByReferenceIdAsync(
        string referenceId,
        CancellationToken cancellationToken)
    {
        var page = await _client.Payouts.ListAsync(
            new GetPayoutListParam { ReferenceId = referenceId, Limit = 10, Offset = 0 },
            new RequestOptions { CancellationToken = cancellationToken, MaxRetries = 0 });

        return page.Data.FirstOrDefault(payout =>
            string.Equals(payout.ReferenceId, referenceId, StringComparison.Ordinal));
    }

    internal static PayoutProviderResult Map(Payout payout)
    {
        var transaction = payout.Transactions?.FirstOrDefault();
        var rawStatus = transaction is null
            ? payout.ApprovalState.ToString()
            : $"{payout.ApprovalState}:{transaction.State}";
        var transactionCode = transaction?.Reference ?? transaction?.Id;
        var failureReason = transaction?.ErrorMessage ?? transaction?.ErrorCode;

        var outcome = payout.ApprovalState switch
        {
            PayoutApprovalState.Completed when payout.Transactions is { Count: > 0 } &&
                payout.Transactions.All(item => item.State == PayoutTransactionState.Succeeded)
                => PayoutProviderOutcome.Succeeded,
            PayoutApprovalState.Rejected or PayoutApprovalState.Cancelled or PayoutApprovalState.Failed
                => PayoutProviderOutcome.Failed,
            PayoutApprovalState.Processing or PayoutApprovalState.Approved or PayoutApprovalState.Scheduled
                => PayoutProviderOutcome.Accepted,
            PayoutApprovalState.Drafting or PayoutApprovalState.Submitted
                => PayoutProviderOutcome.Pending,
            _ => PayoutProviderOutcome.SyncRequired
        };

        return new PayoutProviderResult(
            outcome,
            payout.Id,
            transactionCode,
            rawStatus,
            failureReason);
    }

    internal static PayoutProviderResult MapException(string operation, Exception exception)
    {
        if (exception is ConnectionTimeoutException or TimeoutException)
        {
            return SyncRequired($"PayOS {operation} timed out.", "TIMEOUT");
        }

        if (exception is ConnectionException)
        {
            return SyncRequired($"PayOS {operation} failed: network connection unavailable.", "NETWORK_ERROR");
        }

        var apiException = exception as ApiException;
        int? statusCode = apiException is null
            ? null
            : Convert.ToInt32(apiException.StatusCode, CultureInfo.InvariantCulture);
        var providerCode = SanitizeMessage(apiException?.ErrorCode);
        var providerMessage = SanitizeMessage(apiException?.Message);

        var safeMessage = statusCode switch
        {
            401 => "PayOS rejected the payout channel credentials.",
            403 => "PayOS denied access. Whitelist the backend outbound IP in the payout channel.",
            _ when !string.IsNullOrWhiteSpace(providerMessage) => providerMessage,
            _ => $"PayOS {operation} failed ({exception.GetType().Name})."
        };
        var rawStatus = statusCode.HasValue
            ? $"HTTP_{statusCode.Value}"
            : providerCode ?? exception.GetType().Name;

        return SyncRequired($"PayOS {operation} failed: {safeMessage}", rawStatus);
    }

    private static string? SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        var sanitized = string.Join(' ', message.Split(
            ['\r', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return sanitized.Length <= 240 ? sanitized : sanitized[..240];
    }

    private static PayoutProviderResult SyncRequired(string reason, string? rawStatus = null)
    {
        return new PayoutProviderResult(
            PayoutProviderOutcome.SyncRequired,
            null,
            null,
            rawStatus,
            reason);
    }
}
