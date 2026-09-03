using Application.Common.InternalServices.Wallets.Models;
using Application.Common.InternalServices.Wallets.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PayOS;
using PayOS.Exceptions;
using PayOS.Models;
using PayOS.Models.V1.Payouts;

namespace Infrastructure.ExternalServices.Payments.PayOs;

public sealed partial class PayOsPayoutProvider : IPayoutProvider
{
    private const string AvailabilityCacheKey = "payos:payout:availability";
    private readonly PayOSClient _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PayOsPayoutProvider> _logger;

    public PayOsPayoutProvider(
        PayOSClient client,
        IMemoryCache cache,
        ILogger<PayOsPayoutProvider>? logger = null)
    {
        _client = client;
        _cache = cache;
        _logger = logger ?? NullLogger<PayOsPayoutProvider>.Instance;
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
                _logger.LogInformation(
                    "PayOS payout {ReferenceId} already exists as {PayoutId}; reusing it instead of creating a duplicate.",
                    request.ProviderOrderCode,
                    existing.Id);
                return LogResult("payout request", request.ProviderOrderCode, existing);
            }

            _logger.LogInformation(
                "Creating PayOS payout {ReferenceId} for {AmountVnd} VND to BIN {BankBin}.",
                request.ProviderOrderCode,
                request.AmountVnd,
                request.BankBin);

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

            return LogResult("payout request", request.ProviderOrderCode, payout);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimeout("payout request", request.ProviderOrderCode);
            return SyncRequired("PayOS payout request timed out.", "TIMEOUT");
        }
        catch (UserAbortException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogProviderException("payout request", request.ProviderOrderCode, ex);
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

            if (payout is null)
            {
                _logger.LogWarning(
                    "PayOS has no payout for reference {ReferenceId}; the payout was never created.",
                    request.ProviderOrderCode);
                return SyncRequired("PayOS payout was not found by reference ID.");
            }

            return LogResult("payout status request", request.ProviderOrderCode, payout);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimeout("payout status request", request.ProviderOrderCode);
            return SyncRequired("PayOS payout status request timed out.", "TIMEOUT");
        }
        catch (UserAbortException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogProviderException("payout status request", request.ProviderOrderCode, ex);
            return MapException("payout status request", ex);
        }
    }

    public async Task<PayoutProviderAvailability> CheckAvailabilityAsync(
        CancellationToken cancellationToken,
        bool bypassCache = false)
    {
        if (!bypassCache &&
            _cache.TryGetValue<PayoutProviderAvailability>(AvailabilityCacheKey, out var cached))
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
            LogTimeout("payout availability check", null);
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
            LogProviderException("payout availability check", null, ex);
            var mapped = MapException("payout availability check", ex);
            result = new PayoutProviderAvailability(
                false,
                null,
                mapped.RawStatus,
                mapped.FailureReason);
        }

        LogAvailability(result, bypassCache);
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
        int? statusCode = ReadStatusCode(apiException);
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

    /// <summary>
    /// Logs the mapped outcome of a PayOS payout. An outcome of
    /// <see cref="PayoutProviderOutcome.SyncRequired"/> here means PayOS reported an approval
    /// state this code does not understand — for example a channel that requires manual
    /// approval — which otherwise leaves the withdrawal stuck with no explanation.
    /// </summary>
    private PayoutProviderResult LogResult(string operation, string referenceId, Payout payout)
    {
        var result = Map(payout);
        if (result.Outcome == PayoutProviderOutcome.SyncRequired)
        {
            _logger.LogWarning(
                "PayOS {Operation} for {ReferenceId} returned an unhandled approval state. " +
                "PayoutId={PayoutId} RawStatus={RawStatus} FailureReason={FailureReason}",
                operation,
                referenceId,
                payout.Id,
                result.RawStatus,
                SanitizeMessage(result.FailureReason));
        }
        else
        {
            _logger.LogInformation(
                "PayOS {Operation} for {ReferenceId} mapped to {Outcome}. " +
                "PayoutId={PayoutId} RawStatus={RawStatus}",
                operation,
                referenceId,
                result.Outcome,
                payout.Id,
                result.RawStatus);
        }

        return result;
    }

    private void LogAvailability(PayoutProviderAvailability availability, bool bypassCache)
    {
        if (availability.IsAvailable)
        {
            _logger.LogInformation(
                "PayOS payout account is available. BalanceVnd={BalanceVnd} BypassCache={BypassCache}",
                availability.BalanceVnd,
                bypassCache);
            return;
        }

        _logger.LogWarning(
            "PayOS payout account is UNAVAILABLE. ErrorCode={ErrorCode} Reason={Reason} BypassCache={BypassCache}. " +
            "Withdrawals cannot be created or processed until this clears.",
            availability.ErrorCode,
            availability.SafeMessage,
            bypassCache);
    }

    private void LogTimeout(string operation, string? referenceId)
    {
        _logger.LogError(
            "PayOS {Operation} timed out for {ReferenceId}.",
            operation,
            referenceId ?? "(n/a)");
    }

    /// <summary>
    /// Logs the concrete cause of a PayOS failure. The raw exception is deliberately not passed
    /// to the logger: a proxy connection failure surfaces the proxy URL — credentials included —
    /// in <see cref="Exception.Message"/>. The structured fields below carry everything needed to
    /// tell a 401 (bad credentials) from a 403 (outbound IP not whitelisted) from a network fault.
    /// </summary>
    private void LogProviderException(string operation, string? referenceId, Exception exception)
    {
        var apiException = exception as ApiException;
        _logger.LogError(
            "PayOS {Operation} failed for {ReferenceId}. " +
            "ExceptionType={ExceptionType} HttpStatusCode={HttpStatusCode} " +
            "PayOsErrorCode={PayOsErrorCode} Detail={Detail}",
            operation,
            referenceId ?? "(n/a)",
            exception.GetType().Name,
            ReadStatusCode(apiException),
            SanitizeMessage(apiException?.ErrorCode),
            SanitizeMessage(exception.Message));
    }

    private static int? ReadStatusCode(ApiException? apiException) =>
        apiException is null
            ? null
            : Convert.ToInt32(apiException.StatusCode, CultureInfo.InvariantCulture);

    private static string? SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        var sanitized = string.Join(' ', message.Split(
            ['\r', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        sanitized = UrlCredentialsPattern().Replace(sanitized, "$1://***:***@");
        return sanitized.Length <= 240 ? sanitized : sanitized[..240];
    }

    /// <summary>Matches the <c>user:password@</c> segment of a URL so proxy credentials never
    /// reach a log sink or an API response.</summary>
    [GeneratedRegex(@"(\w+)://[^/\s:@]+:[^/\s@]+@", RegexOptions.IgnoreCase)]
    private static partial Regex UrlCredentialsPattern();

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
