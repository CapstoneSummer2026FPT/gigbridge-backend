using Application.Common.InternalServices.Receipts.Services;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Escrow;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.Wallets;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Receipts;

public sealed class ProjectReceiptWorkflowTests
{
    [Fact]
    public async Task EnsurePair_IncludesNonstandardReleasesAndAcceptanceFeeInSnapshot()
    {
        var now = DateTime.UtcNow;
        var contractId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var client = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Client",
            Email = "client@example.com"
        };
        var freelancer = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Freelancer",
            Email = "freelancer@example.com"
        };
        var contract = new Contract
        {
            ContractsId = contractId,
            ClientProfilesId = clientProfileId,
            FreelancerProfilesId = freelancerProfileId,
            Title = "Admin-released project",
            Status = (int)ContractStatus.Completed,
            CompletedAt = now,
            CreatedAt = now
        };
        var escrowId = Guid.NewGuid();
        var adminMilestoneId = Guid.NewGuid();
        var disputeMilestoneId = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();
        context.AddSet<ProjectReceipt>();
        context.AddSet(contract);
        context.AddSet(new ClientProfile
        {
            ClientProfilesId = clientProfileId,
            UserId = client.UserId,
            CompanyName = "Client company"
        });
        context.AddSet(new FreelancerProfile
        {
            FreelancerProfilesId = freelancerProfileId,
            UserId = freelancer.UserId
        });
        context.AddSet(client, freelancer);
        context.AddSet(new ContractEscrow
        {
            ContractEscrowId = escrowId,
            ContractsId = contractId,
            RequiredAmount = 100m,
            FundedAmount = 100m,
            ReleasedAmount = 100m,
            Status = (int)ContractEscrowStatus.Released
        });
        context.AddSet(
            new Milestone
            {
                MilestonesId = adminMilestoneId,
                ContractsId = contractId,
                Title = "Admin release",
                Amount = 40m,
                ReleasedAmount = 40m,
                Status = (int)MilestoneStatus.Approved,
                SortOrder = 1,
                ApprovedAt = now,
                CreatedAt = now
            },
            new Milestone
            {
                MilestonesId = disputeMilestoneId,
                ContractsId = contractId,
                Title = "Dispute release",
                Amount = 60m,
                ReleasedAmount = 60m,
                Status = (int)MilestoneStatus.Approved,
                SortOrder = 2,
                ApprovedAt = now,
                CreatedAt = now
            });
        context.AddSet(
            new EscrowTransaction
            {
                EscrowTransactionId = Guid.NewGuid(),
                ContractEscrowId = escrowId,
                MilestonesId = adminMilestoneId,
                Amount = 40m,
                Type = (int)EscrowTransactionType.ReleaseToFreelancer,
                Status = (int)EscrowTransactionStatus.Succeeded,
                GatewayTransactionCode = $"ESCROW-FORCE-RELEASE-{escrowId:N}-{adminMilestoneId:N}",
                CreatedAt = now,
                CompletedAt = now
            },
            new EscrowTransaction
            {
                EscrowTransactionId = Guid.NewGuid(),
                ContractEscrowId = escrowId,
                MilestonesId = disputeMilestoneId,
                Amount = 60m,
                Type = (int)EscrowTransactionType.ReleaseToFreelancer,
                Status = (int)EscrowTransactionStatus.Succeeded,
                GatewayTransactionCode = $"DISPUTE-RELEASE-{contractId:N}-{disputeMilestoneId:N}",
                CreatedAt = now,
                CompletedAt = now
            });
        context.AddSet(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserId = freelancer.UserId,
            ContractsId = contractId,
            TokenAmount = 1m,
            Type = (int)WalletTransactionType.Adjustment,
            Status = (int)WalletTransactionStatus.Succeeded,
            IdempotencyKey = $"{ServiceFeeWorkflow.AcceptJobFeePrefix}{contractId:N}",
            CreatedAt = now,
            CompletedAt = now
        });

        var receipts = await ProjectReceiptWorkflow.EnsurePairAsync(
            context,
            contractId,
            now,
            CancellationToken.None);

        var snapshot = ProjectReceiptWorkflow.DeserializeSnapshot(receipts[0]);
        Assert.Equal(1m, snapshot.FreelancerServiceFeeGigCoin);
        Assert.Equal(99m, snapshot.FreelancerNetReceivedGigCoin);
        Assert.Collection(
            snapshot.Milestones,
            milestone =>
            {
                Assert.Equal(0.4m, milestone.ServiceFeeGigCoin);
                Assert.Equal(39.6m, milestone.NetReceivedGigCoin);
            },
            milestone =>
            {
                Assert.Equal(0.6m, milestone.ServiceFeeGigCoin);
                Assert.Equal(59.4m, milestone.NetReceivedGigCoin);
            });
        Assert.Equal(100m, snapshot.TotalReleasedBeforeCompletionGigCoin);
        Assert.Equal(0m, snapshot.FinalReleaseGigCoin);
    }

    [Fact]
    public async Task EnsurePair_RebuildsStaleSnapshotsAndQueuesTimelineTemplateRegeneration()
    {
        var now = DateTime.UtcNow;
        var contractId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var client = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Client",
            Email = "client@example.com"
        };
        var freelancer = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Freelancer",
            Email = "freelancer@example.com"
        };
        var milestoneId = Guid.NewGuid();
        var escrowId = Guid.NewGuid();
        var startedAt = now.AddDays(-2);
        var dueDate = DateOnly.FromDateTime(now.AddDays(-1));
        var context = new InMemoryApplicationDbContext();
        var receipts = context.AddSet(
            CreateReadyReceipt(contractId, ProjectReceiptType.Client),
            CreateReadyReceipt(contractId, ProjectReceiptType.Freelancer));
        context.AddSet(new Contract
        {
            ContractsId = contractId,
            ClientProfilesId = clientProfileId,
            FreelancerProfilesId = freelancerProfileId,
            Title = "Timeline project",
            TotalBudget = 100m,
            Status = (int)ContractStatus.Completed,
            CompletedAt = now,
            CreatedAt = startedAt
        });
        context.AddSet(new ClientProfile
        {
            ClientProfilesId = clientProfileId,
            UserId = client.UserId,
            CompanyName = "Client company"
        });
        context.AddSet(new FreelancerProfile
        {
            FreelancerProfilesId = freelancerProfileId,
            UserId = freelancer.UserId
        });
        context.AddSet(client, freelancer);
        context.AddSet(new ContractEscrow
        {
            ContractEscrowId = escrowId,
            ContractsId = contractId,
            RequiredAmount = 100m,
            FundedAmount = 100m,
            ReleasedAmount = 100m,
            Status = (int)ContractEscrowStatus.Released
        });
        context.AddSet(new Milestone
        {
            MilestonesId = milestoneId,
            ContractsId = contractId,
            Title = "Delivery",
            Amount = 100m,
            ReleasedAmount = 100m,
            Status = (int)MilestoneStatus.Approved,
            SortOrder = 1,
            StartedAt = startedAt,
            DueDate = dueDate,
            ApprovedAt = now.AddHours(-1),
            CreatedAt = startedAt
        });
        context.AddSet(new EscrowTransaction
        {
            EscrowTransactionId = Guid.NewGuid(),
            ContractEscrowId = escrowId,
            MilestonesId = milestoneId,
            Amount = 100m,
            Type = (int)EscrowTransactionType.ReleaseToFreelancer,
            Status = (int)EscrowTransactionStatus.Succeeded,
            GatewayTransactionCode = $"ESCROW-FINAL-{contractId:N}-{milestoneId:N}",
            CreatedAt = now,
            CompletedAt = now
        });

        var result = await ProjectReceiptWorkflow.EnsurePairAsync(
            context,
            contractId,
            now,
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(receipts.Entities, receipt =>
        {
            Assert.Equal(4, receipt.TemplateVersion);
            Assert.Equal((int)ProjectReceiptGenerationStatus.Pending, receipt.GenerationStatus);
            Assert.Equal((int)ProjectReceiptEmailStatus.Pending, receipt.EmailStatus);
            Assert.NotEqual("{}", receipt.SnapshotJson);
            Assert.Null(receipt.PdfContent);
            Assert.Null(receipt.GeneratedAt);
            Assert.Null(receipt.EmailedAt);

            var snapshot = ProjectReceiptWorkflow.DeserializeSnapshot(receipt);
            Assert.Equal(startedAt, snapshot.ProjectStartedAtUtc);
            var milestone = Assert.Single(snapshot.Milestones);
            Assert.Equal(startedAt, milestone.StartedAtUtc);
            Assert.Equal(dueDate, milestone.DueDate);
        });
    }

    private static ProjectReceipt CreateReadyReceipt(Guid contractId, ProjectReceiptType type) => new()
    {
        ProjectReceiptId = Guid.NewGuid(),
        ContractsId = contractId,
        OwnerUserId = Guid.NewGuid(),
        ReceiptType = (int)type,
        ReceiptNumber = $"TEST-{type}",
        TemplateVersion = 1,
        IssuedAt = DateTime.UtcNow,
        SnapshotJson = "{}",
        SnapshotHashSha256 = new string('a', 64),
        GenerationStatus = (int)ProjectReceiptGenerationStatus.Ready,
        NextGenerationAttemptAt = DateTime.UtcNow,
        PdfContent = [1, 2, 3],
        GeneratedAt = DateTime.UtcNow,
        EmailStatus = (int)ProjectReceiptEmailStatus.Delivered,
        NextEmailAttemptAt = DateTime.UtcNow,
        EmailedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };
}
