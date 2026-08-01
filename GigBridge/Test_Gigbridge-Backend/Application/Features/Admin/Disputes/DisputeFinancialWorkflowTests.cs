using Application.Common.Exceptions;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Admin.Disputes;

public sealed class DisputeFinancialWorkflowTests
{
    [Fact]
    public void Penalty_MovesHeldTokensToSystemWalletAndCreatesTwoIdempotentLedgerEntries()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var client = new UserWallet { UserWalletsId = Guid.NewGuid(), UserId = Guid.NewGuid(), HeldTokens = 80m };
        var system = new UserWallet { UserWalletsId = Guid.NewGuid(), UserId = DisputePenaltyAccount.UserId };
        var contractId = Guid.NewGuid();
        var escrowId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
        var code = $"DISPUTE-PENALTY-{disputeId:N}-{milestoneId:N}";

        var result = ContractEscrowWalletWorkflow.Penalty(
            context, client, system, contractId, escrowId, milestoneId, disputeId,
            25_000m, code, "Confirmed financial misconduct", DateTime.UtcNow);

        Assert.Equal(55m, client.HeldTokens);
        Assert.Equal(25m, system.AvailableTokens);
        Assert.Equal(2, transactions.Entities.Count);
        Assert.All(transactions.Entities, item =>
        {
            Assert.Equal((int)WalletTransactionType.DisputePenalty, item.Type);
            Assert.Equal(code, item.IdempotencyKey);
            Assert.Equal(disputeId.ToString(),
                System.Text.Json.JsonDocument.Parse(item.Metadata!).RootElement.GetProperty("disputeId").GetString());
        });
        Assert.Equal(system.UserId, result.SystemCredit.UserId);
    }

    [Fact]
    public void Penalty_WithInsufficientHeldTokensDoesNotMutateWalletsOrLedger()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var client = new UserWallet { UserWalletsId = Guid.NewGuid(), UserId = Guid.NewGuid(), HeldTokens = 5m };
        var system = new UserWallet { UserWalletsId = Guid.NewGuid(), UserId = DisputePenaltyAccount.UserId };

        Assert.Throws<BadRequestException>(() => ContractEscrowWalletWorkflow.Penalty(
            context, client, system, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10_000m, "penalty", "reason", DateTime.UtcNow));

        Assert.Equal(5m, client.HeldTokens);
        Assert.Equal(0m, system.AvailableTokens);
        Assert.Empty(transactions.Entities);
    }

    [Fact]
    public void ContractLockKey_IsStableAndDifferentAcrossContracts()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        Assert.Equal(ContractEscrowLock.ForContract(first), ContractEscrowLock.ForContract(first));
        Assert.NotEqual(ContractEscrowLock.ForContract(first), ContractEscrowLock.ForContract(second));
    }
}
