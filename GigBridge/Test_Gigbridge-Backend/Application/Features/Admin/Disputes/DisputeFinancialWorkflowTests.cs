using Application.Common.Exceptions;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums.Wallets;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Admin.Disputes;

public sealed class DisputeFinancialWorkflowTests
{
    [Fact]
    public void Penalty_DebitsOnlyClientHeldTokensAndCreatesOneLedgerEntryWithIdempotentReference()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var client = new UserWallet { UserWalletsId = Guid.NewGuid(), UserId = Guid.NewGuid(), HeldTokens = 80m };
        var contractId = Guid.NewGuid();
        var escrowId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
        var code = $"DISPUTE-PENALTY-DEBIT-{disputeId:N}-{milestoneId:N}";

        var escrow = new ContractEscrow
        {
            ContractEscrowId = escrowId,
            ContractsId = contractId,
            DepositedTokens = 80m,
            EarnedTokens = 0m
        };

        var clientDebit = ContractEscrowWalletWorkflow.Penalty(
            context, client, escrow, contractId, milestoneId, disputeId,
            25m, code, "Confirmed financial misconduct", DateTime.UtcNow);

        Assert.Equal(55m, client.HeldTokens);
        var transaction = Assert.Single(transactions.Entities);
        Assert.Same(clientDebit, transaction);
        Assert.Equal(client.UserId, transaction.UserId);
        Assert.Equal(client.UserWalletsId, transaction.UserWalletsId);
        Assert.Equal((int)WalletTransactionType.DisputePenalty, transaction.Type);
        Assert.Equal(code, transaction.IdempotencyKey);
        using var metadata = System.Text.Json.JsonDocument.Parse(transaction.Metadata!);
        Assert.Equal(disputeId.ToString(), metadata.RootElement.GetProperty("disputeId").GetString());
        Assert.True(metadata.RootElement.GetProperty("retainedByPlatform").GetBoolean());
        Assert.False(metadata.RootElement.TryGetProperty("destinationUserId", out _));

        var penalty = new DisputePenalty
        {
            ClientDebitWalletTransactionId = clientDebit.WalletTransactionsId,
            ClientDebitWalletTransaction = clientDebit
        };
        Assert.Equal(clientDebit.WalletTransactionsId, penalty.ClientDebitWalletTransactionId);
        Assert.Same(clientDebit, penalty.ClientDebitWalletTransaction);
    }

    [Fact]
    public void Penalty_WithInsufficientHeldTokensDoesNotMutateWalletsOrLedger()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var client = new UserWallet { UserWalletsId = Guid.NewGuid(), UserId = Guid.NewGuid(), HeldTokens = 5m };

        Assert.Throws<BadRequestException>(() => ContractEscrowWalletWorkflow.Penalty(
            context, client, new ContractEscrow(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10m, "penalty", "reason", DateTime.UtcNow));

        Assert.Equal(5m, client.HeldTokens);
        Assert.Empty(transactions.Entities);
    }

    [Fact]
    public void DisputeHistoryAndFinancialLedgerRelationshipsUseRestrictDeleteBehavior()
    {
        using var db = new GigbridgeDbContext(new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var penalty = db.Model.FindEntityType(typeof(DisputePenalty))!;
        var violation = db.Model.FindEntityType(typeof(UserViolation))!;

        Assert.Equal(DeleteBehavior.Restrict, penalty.GetForeignKeys()
            .Single(item => item.PrincipalEntityType.ClrType == typeof(Dispute)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, violation.GetForeignKeys()
            .Single(item => item.PrincipalEntityType.ClrType == typeof(Dispute)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, penalty.GetForeignKeys()
            .Single(item => item.PrincipalEntityType.ClrType == typeof(WalletTransaction)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, penalty.GetForeignKeys()
            .Single(item => item.PrincipalEntityType.ClrType == typeof(EscrowTransaction)).DeleteBehavior);
        Assert.True(penalty.GetIndexes().Single(item =>
            item.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(DisputePenalty.DisputeId), nameof(DisputePenalty.MilestoneId) })).IsUnique);
    }

    [Fact]
    public void MigrationContainsSchemaOperationsOnlyAndNoUserOrWalletSeedData()
    {
        var migration = new ImproveAdminDisputeResolution();

        Assert.DoesNotContain(migration.UpOperations, operation =>
            operation is InsertDataOperation or UpdateDataOperation or DeleteDataOperation or SqlOperation);
        Assert.DoesNotContain(migration.DownOperations, operation =>
            operation is InsertDataOperation or UpdateDataOperation or DeleteDataOperation or SqlOperation);
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
