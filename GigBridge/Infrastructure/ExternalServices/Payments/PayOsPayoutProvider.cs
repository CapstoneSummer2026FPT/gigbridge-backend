using Application.Common.Interfaces.IService;
using PayOS;
using PayOS.Models;
using PayOS.Models.V1.Payouts;

namespace Infrastructure.ExternalServices.Payments;

public sealed class PayOsPayoutProvider : IPayoutProvider
{
    private readonly PayOSClient _client;

    public PayOsPayoutProvider(PayOSClient client)
    {
        _client = client;
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
            return SyncRequired("PayOS payout request timed out.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SyncRequired($"PayOS payout request failed ({ex.GetType().Name}).");
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
            return SyncRequired("PayOS payout status request timed out.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SyncRequired($"PayOS payout status request failed ({ex.GetType().Name}).");
        }
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

    private static PayoutProviderResult SyncRequired(string reason)
    {
        return new PayoutProviderResult(
            PayoutProviderOutcome.SyncRequired,
            null,
            null,
            null,
            reason);
    }
}
