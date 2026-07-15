using Application.Common.Interfaces.IService;
using Infrastructure.ExternalServices.Payments;
using PayOS.Models.V1.Payouts;

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
}
