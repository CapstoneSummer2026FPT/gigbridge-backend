using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Amendments.Commands;
using Application.Features.Contracts.Amendments.DTOs;
using Application.Features.Contracts.Details.Client.Update.DTOs;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Common;

public sealed class ContractAmendmentWorkflowTests
{
    [Fact]
    public async Task ChangeRequest_ClarificationRound_ReturnsToCounterpartyForDecision()
    {
        var fixture = new AmendmentFixture();
        var create = new CreateContractChangeRequestCommandHandler(fixture.Context, fixture.Clock);
        var respond = new RespondContractChangeRequestCommandHandler(fixture.Context, fixture.Clock);
        var requestId = await create.Handle(new CreateContractChangeRequestCommand(
            fixture.ContractId,
            fixture.FreelancerUserId,
            new CreateContractChangeRequest(
                "Clarify future scope",
                "Replace the reporting work item.",
                [fixture.PendingMilestoneId],
                [fixture.PendingWorkItemId])), CancellationToken.None);

        await respond.Handle(new RespondContractChangeRequestCommand(
            fixture.ContractId,
            requestId,
            fixture.ClientUserId,
            new RespondContractChangeRequest(false, true, "Which report format is required?")), CancellationToken.None);

        var request = fixture.Context.Set<ContractChangeRequest>().Single(item => item.ContractChangeRequestId == requestId);
        Assert.Equal((int)ContractChangeRequestStatus.NeedsClarification, request.Status);
        Assert.Equal("Which report format is required?", request.ClarificationRequestNote);

        await respond.Handle(new RespondContractChangeRequestCommand(
            fixture.ContractId,
            requestId,
            fixture.FreelancerUserId,
            new RespondContractChangeRequest(false, false, "A CSV export and dashboard view.")), CancellationToken.None);
        Assert.Equal((int)ContractChangeRequestStatus.Pending, request.Status);
        Assert.Equal("A CSV export and dashboard view.", request.ClarificationResponseNote);
        Assert.NotNull(request.ClarifiedAt);

        await respond.Handle(new RespondContractChangeRequestCommand(
            fixture.ContractId,
            requestId,
            fixture.ClientUserId,
            new RespondContractChangeRequest(true, false, "Scope is clear.")), CancellationToken.None);
        Assert.Equal((int)ContractChangeRequestStatus.Accepted, request.Status);
        Assert.Equal("Scope is clear.", request.ResponseNote);
    }

    [Fact]
    public async Task AmendmentIncrease_AppliesOnlyAfterTwoSignaturesAndDeltaFunding()
    {
        var fixture = new AmendmentFixture();
        var amendment = await fixture.CreateAcceptedAmendmentAsync(800m);
        var respond = new RespondContractAmendmentCommandHandler(fixture.Context);
        var sign = new SignContractAmendmentCommandHandler(fixture.Context, fixture.Clock);

        await respond.Handle(new RespondContractAmendmentCommand(
            fixture.ContractId, amendment.ContractAmendmentId, fixture.FreelancerUserId,
            new RespondContractAmendmentRequest(true, false, "Plan approved.")), CancellationToken.None);
        Assert.Equal((int)ContractAmendmentStatus.PendingSignatures, amendment.Status);
        Assert.False(string.IsNullOrWhiteSpace(amendment.DocumentSnapshotJson));

        await sign.Handle(new SignContractAmendmentCommand(
            fixture.ContractId, amendment.ContractAmendmentId, fixture.ClientUserId,
            new SignContractAmendmentRequest("Client Signature")), CancellationToken.None);
        Assert.Equal((int)ContractAmendmentStatus.PendingSignatures, amendment.Status);

        await sign.Handle(new SignContractAmendmentCommand(
            fixture.ContractId, amendment.ContractAmendmentId, fixture.FreelancerUserId,
            new SignContractAmendmentRequest("Freelancer Signature")), CancellationToken.None);
        Assert.Equal((int)ContractAmendmentStatus.PendingFunding, amendment.Status);
        Assert.Equal(1_000m, fixture.Contract.TotalBudget);

        var fund = new FundContractAmendmentCommandHandler(fixture.Context, fixture.Clock);
        await fund.Handle(new FundContractAmendmentCommand(
            fixture.ContractId, amendment.ContractAmendmentId, fixture.ClientUserId), CancellationToken.None);

        Assert.Equal((int)ContractAmendmentStatus.Applied, amendment.Status);
        Assert.Equal(1_200m, fixture.Contract.TotalBudget);
        Assert.Equal(2, fixture.Contract.RevisionNumber);
        Assert.Equal(1_200m, fixture.Escrow.FundedAmount);
        Assert.Equal(1_200m, fixture.Escrow.RequiredAmount);
        Assert.Equal(0.798m, fixture.ClientWallet.AvailableTokens);
        Assert.Equal(1.2m, fixture.ClientWallet.HeldTokens);
        Assert.Contains(fixture.Context.Set<WalletTransaction>(), item =>
            item.GatewayTransactionCode == $"AMENDMENT-FUND-{amendment.ContractAmendmentId:N}");
    }

    [Fact]
    public async Task AmendmentDecrease_RefundsHeldEscrowBeforeApplyingFuturePlan()
    {
        var fixture = new AmendmentFixture();
        var amendment = await fixture.CreateAcceptedAmendmentAsync(500m);
        var respond = new RespondContractAmendmentCommandHandler(fixture.Context);
        var sign = new SignContractAmendmentCommandHandler(fixture.Context, fixture.Clock);

        await respond.Handle(new RespondContractAmendmentCommand(
            fixture.ContractId, amendment.ContractAmendmentId, fixture.FreelancerUserId,
            new RespondContractAmendmentRequest(true, false, null)), CancellationToken.None);
        await sign.Handle(new SignContractAmendmentCommand(
            fixture.ContractId, amendment.ContractAmendmentId, fixture.ClientUserId,
            new SignContractAmendmentRequest("Client Signature")), CancellationToken.None);
        await sign.Handle(new SignContractAmendmentCommand(
            fixture.ContractId, amendment.ContractAmendmentId, fixture.FreelancerUserId,
            new SignContractAmendmentRequest("Freelancer Signature")), CancellationToken.None);

        Assert.Equal((int)ContractAmendmentStatus.Applied, amendment.Status);
        Assert.Equal(900m, fixture.Contract.TotalBudget);
        Assert.Equal(900m, fixture.Escrow.FundedAmount);
        Assert.Equal(900m, fixture.Escrow.RequiredAmount);
        Assert.Equal(1.1m, fixture.ClientWallet.AvailableTokens);
        Assert.Equal(0.9m, fixture.ClientWallet.HeldTokens);
        Assert.Contains(fixture.Context.Set<EscrowTransaction>(), item =>
            item.GatewayTransactionCode == $"AMENDMENT-REFUND-{amendment.ContractAmendmentId:N}");
    }

    private sealed class AmendmentFixture
    {
        public AmendmentFixture()
        {
            Clock = new FixedClock(new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc));
            Contract = new Contract
            {
                ContractsId = ContractId,
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = FreelancerProfileId,
                Title = "Contract",
                TotalBudget = 1_000m,
                RevisionNumber = 1,
                Status = (int)ContractStatus.Active,
                CreatedAt = Clock.UtcNow
            };
            var active = new Milestone
            {
                MilestonesId = ActiveMilestoneId,
                ContractsId = ContractId,
                Title = "Active milestone",
                Amount = 400m,
                Status = (int)MilestoneStatus.InProgress,
                SortOrder = 0,
                CreatedAt = Clock.UtcNow
            };
            var pendingWorkItem = new ContractWorkItem
            {
                ContractWorkItemId = PendingWorkItemId,
                MilestonesId = PendingMilestoneId,
                Title = "Reporting",
                Description = "Build reporting.",
                OrderIndex = 0,
                Status = (int)ContractWorkItemStatus.Todo,
                CreatedAt = Clock.UtcNow
            };
            var pending = new Milestone
            {
                MilestonesId = PendingMilestoneId,
                ContractsId = ContractId,
                Title = "Pending milestone",
                Amount = 600m,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 1,
                CreatedAt = Clock.UtcNow,
                WorkItems = [pendingWorkItem]
            };
            Escrow = new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = ContractId,
                RequiredAmount = 1_000m,
                FundedAmount = 1_000m,
                ReleasedAmount = 0m,
                Status = (int)ContractEscrowStatus.Funded,
                CreatedAt = Clock.UtcNow
            };
            ClientWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(),
                UserId = ClientUserId,
                AvailableTokens = 1m,
                HeldTokens = 1m,
                CreatedAt = Clock.UtcNow
            };

            Context.AddSet(Contract);
            Context.AddSet(active, pending);
            Context.AddSet(pendingWorkItem);
            Context.AddSet(new ClientProfile { ClientProfilesId = ClientProfileId, UserId = ClientUserId });
            Context.AddSet(new FreelancerProfile { FreelancerProfilesId = FreelancerProfileId, UserId = FreelancerUserId });
            Context.AddSet(ClientWallet);
            Context.AddSet(Escrow);
            Context.AddSet<ContractChangeRequest>();
            Context.AddSet<ContractAmendment>();
            Context.AddSet<ContractAmendmentMilestone>();
            Context.AddSet<ContractAmendmentWorkItem>();
            Context.AddSet<ContractAmendmentSignature>();
            Context.AddSet<WalletTransaction>();
            Context.AddSet<EscrowTransaction>();
        }

        public async Task<ContractAmendment> CreateAcceptedAmendmentAsync(decimal futureAmount)
        {
            var changeRequest = new ContractChangeRequest
            {
                ContractChangeRequestId = Guid.NewGuid(),
                ContractsId = ContractId,
                RequestedByUserId = FreelancerUserId,
                Reason = "Adjust future scope",
                RequestedChanges = "Update pending milestone.",
                AffectedMilestoneIds = [PendingMilestoneId],
                AffectedWorkItemIds = [PendingWorkItemId],
                Status = (int)ContractChangeRequestStatus.Accepted,
                CreatedAt = Clock.UtcNow
            };
            Context.Set<ContractChangeRequest>().Add(changeRequest);
            var create = new CreateContractAmendmentCommandHandler(Context, Clock);
            var amendmentId = await create.Handle(new CreateContractAmendmentCommand(
                ContractId,
                ClientUserId,
                new CreateContractAmendmentRequest(
                    changeRequest.ContractChangeRequestId,
                    "Revised future delivery",
                    [new ContractMilestoneRequest(
                        PendingMilestoneId,
                        "Revised milestone",
                        futureAmount,
                        DateOnly.FromDateTime(Clock.UtcNow.AddDays(30)),
                        1,
                        "Updated scope",
                        "4 weeks",
                        "Production release",
                        "Acceptance tests pass",
                        [new ContractWorkItemRequest(PendingWorkItemId, "Revised reporting", "Build revised reporting.", "CSV and dashboard", "2 weeks", 0)])])),
                CancellationToken.None);
            return Context.Set<ContractAmendment>().Single(item => item.ContractAmendmentId == amendmentId);
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public FixedClock Clock { get; }
        public Guid ContractId { get; } = Guid.NewGuid();
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerProfileId { get; } = Guid.NewGuid();
        public Guid ActiveMilestoneId { get; } = Guid.NewGuid();
        public Guid PendingMilestoneId { get; } = Guid.NewGuid();
        public Guid PendingWorkItemId { get; } = Guid.NewGuid();
        public Contract Contract { get; }
        public ContractEscrow Escrow { get; }
        public UserWallet ClientWallet { get; }
    }

    private sealed class FixedClock(DateTime utcNow) : IDateTimeService
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
