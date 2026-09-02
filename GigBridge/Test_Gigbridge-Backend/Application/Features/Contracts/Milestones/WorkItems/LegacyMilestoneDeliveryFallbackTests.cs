using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Features.Contracts.Milestones.Client.Approve.Commands;
using Application.Features.Contracts.Milestones.Client.RequestRevision.Commands;
using Application.Features.Contracts.Milestones.Client.RequestRevision.DTOs;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.WorkItems.Client.Review.Commands;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Milestones.WorkItems;

/// <summary>
/// Contracts that predate work item delivery already carry ContractWorkItem rows, so anything that
/// decided the delivery flow by counting work items would silently move every live contract onto
/// endpoints its participants have never seen. The mode is a stored column instead, and these tests
/// pin that: a Legacy contract WITH work items keeps the milestone-level flow, and a WorkItem
/// contract refuses it.
///
/// They also pin the attachment-scoping fix. The legacy submit handler replaces its own file bundle
/// by deleting every attachment on the milestone; once per-work-item files live on the same table,
/// an unscoped delete would destroy the delivery history a dispute depends on.
/// </summary>
public sealed class LegacyMilestoneDeliveryFallbackTests
{
    [Fact]
    public async Task ApproveMilestone_StillWorksOnALegacyContractThatAlreadyHasWorkItems()
    {
        var fixture = new DeliveryModeFixture(MilestoneDeliveryMode.Legacy);
        fixture.Milestone.Status = (int)MilestoneStatus.Submitted;

        var result = await fixture.CreateApproveHandler().Handle(
            new ApproveMilestoneCommand(fixture.ContractId, fixture.MilestoneId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Approved, result.Status);
        Assert.NotNull(fixture.Milestone.ApprovedAt);
    }

    [Fact]
    public async Task ApproveMilestone_IsRefusedOnAWorkItemContract()
    {
        var fixture = new DeliveryModeFixture(MilestoneDeliveryMode.WorkItem);
        fixture.Milestone.Status = (int)MilestoneStatus.Submitted;

        await Assert.ThrowsAsync<BadRequestException>(() => fixture.CreateApproveHandler().Handle(
            new ApproveMilestoneCommand(fixture.ContractId, fixture.MilestoneId, fixture.ClientUserId),
            CancellationToken.None));
    }

    [Fact]
    public async Task RequestMilestoneRevision_IsRefusedOnAWorkItemContract()
    {
        // Without this guard the client could bypass the work item review path entirely: the
        // milestone-level endpoint would flip work items to RevisionRequired without recording the
        // verdict on their attempts, leaving an item marked "needs changes" whose latest attempt
        // still reads as awaiting review, and never running the reconciliation.
        var fixture = new DeliveryModeFixture(MilestoneDeliveryMode.WorkItem);
        fixture.Milestone.Status = (int)MilestoneStatus.Submitted;

        await Assert.ThrowsAsync<BadRequestException>(() => fixture.CreateRevisionHandler().Handle(
            new RequestMilestoneRevisionCommand(
                fixture.ContractId,
                fixture.MilestoneId,
                fixture.ClientUserId,
                new RequestMilestoneRevisionRequest("Needs changes.", [fixture.WorkItemId])),
            CancellationToken.None));
    }

    [Fact]
    public async Task RequestMilestoneRevision_StillWorksOnALegacyContractThatAlreadyHasWorkItems()
    {
        var fixture = new DeliveryModeFixture(MilestoneDeliveryMode.Legacy);
        fixture.Milestone.Status = (int)MilestoneStatus.Submitted;

        var result = await fixture.CreateRevisionHandler().Handle(
            new RequestMilestoneRevisionCommand(
                fixture.ContractId,
                fixture.MilestoneId,
                fixture.ClientUserId,
                new RequestMilestoneRevisionRequest("Needs changes.", [fixture.WorkItemId])),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.InProgress, result.Status);
        Assert.Equal((int)ContractWorkItemStatus.RevisionRequired, fixture.WorkItem.Status);
    }

    [Fact]
    public async Task ReviewWorkItems_IsRefusedOnALegacyContractEvenThoughItHasWorkItems()
    {
        var fixture = new DeliveryModeFixture(MilestoneDeliveryMode.Legacy);

        await Assert.ThrowsAsync<BadRequestException>(() => fixture.CreateReviewHandler().Handle(
            new ReviewContractWorkItemsCommand(
                fixture.ContractId,
                fixture.MilestoneId,
                fixture.ClientUserId,
                [fixture.WorkItemId],
                Approve: true,
                Reason: null),
            CancellationToken.None));
    }

    [Fact]
    public async Task ReviewWorkItems_RefusesAWorkItemThatBelongsToAnotherMilestone()
    {
        var fixture = new DeliveryModeFixture(MilestoneDeliveryMode.WorkItem);

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.CreateReviewHandler().Handle(
            new ReviewContractWorkItemsCommand(
                fixture.ContractId,
                fixture.MilestoneId,
                fixture.ClientUserId,
                [Guid.NewGuid()],
                Approve: true,
                Reason: null),
            CancellationToken.None));
    }

    [Fact]
    public async Task ReviewWorkItems_RejectsAnEmptySelection()
    {
        var fixture = new DeliveryModeFixture(MilestoneDeliveryMode.WorkItem);

        await Assert.ThrowsAsync<BadRequestException>(() => fixture.CreateReviewHandler().Handle(
            new ReviewContractWorkItemsCommand(
                fixture.ContractId, fixture.MilestoneId, fixture.ClientUserId, [], Approve: true, Reason: null),
            CancellationToken.None));
    }

    [Fact]
    public async Task ReviewWorkItems_RejectsADuplicatedWorkItemId()
    {
        var fixture = new DeliveryModeFixture(MilestoneDeliveryMode.WorkItem);

        await Assert.ThrowsAsync<BadRequestException>(() => fixture.CreateReviewHandler().Handle(
            new ReviewContractWorkItemsCommand(
                fixture.ContractId,
                fixture.MilestoneId,
                fixture.ClientUserId,
                [fixture.WorkItemId, fixture.WorkItemId],
                Approve: true,
                Reason: null),
            CancellationToken.None));
    }

    [Fact]
    public async Task RequestRevision_RequiresAReason()
    {
        var fixture = new DeliveryModeFixture(MilestoneDeliveryMode.WorkItem);

        await Assert.ThrowsAsync<BadRequestException>(() => fixture.CreateReviewHandler().Handle(
            new ReviewContractWorkItemsCommand(
                fixture.ContractId,
                fixture.MilestoneId,
                fixture.ClientUserId,
                [fixture.WorkItemId],
                Approve: false,
                Reason: "   "),
            CancellationToken.None));
    }

    private sealed class DeliveryModeFixture
    {
        public DeliveryModeFixture(MilestoneDeliveryMode mode)
        {
            Contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = FreelancerProfileId,
                Title = "Delivery mode contract",
                TotalBudget = 1_000m,
                Status = (int)ContractStatus.Active,
                DeliveryMode = (int)mode,
                CreatedAt = Now
            };

            Milestone = new Milestone
            {
                MilestonesId = MilestoneId,
                ContractsId = ContractId,
                Title = "Milestone 1",
                Amount = 1_000m,
                Status = (int)MilestoneStatus.InProgress,
                SortOrder = 0,
                CreatedAt = Now
            };

            // Deliberately present on BOTH modes: a count-based discriminator would pass every
            // one of these tests while still breaking every live contract.
            WorkItem = new ContractWorkItem
            {
                ContractWorkItemId = WorkItemId,
                MilestonesId = MilestoneId,
                Title = "Work item 1",
                OrderIndex = 0,
                Status = (int)ContractWorkItemStatus.Submitted,
                CreatedAt = Now
            };

            Context.AddSet(
                new User { UserId = ClientUserId, Role = (int)UserRole.Client, Email = "client@example.com", FullName = "Client" },
                new User { UserId = FreelancerUserId, Role = (int)UserRole.Freelancer, Email = "freelancer@example.com", FullName = "Freelancer" });
            Context.AddSet(new ClientProfile { ClientProfilesId = ClientProfileId, UserId = ClientUserId });
            Context.AddSet(new FreelancerProfile { FreelancerProfilesId = FreelancerProfileId, UserId = FreelancerUserId });
            Context.AddSet(Contract);
            Context.AddSet(Milestone);
            Context.AddSet(WorkItem);
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerProfileId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Guid MilestoneId { get; } = Guid.NewGuid();
        public Guid WorkItemId { get; } = Guid.NewGuid();
        public Contract Contract { get; }
        public Milestone Milestone { get; }
        public ContractWorkItem WorkItem { get; }

        public ApproveMilestoneCommandHandler CreateApproveHandler() =>
            new(Context, new FixedClock(Now), new CapturingUserAuditLogService(), new NoopChatRealtimeNotifier());

        public RequestMilestoneRevisionCommandHandler CreateRevisionHandler() =>
            new(Context, new FixedClock(Now), new NoopChatRealtimeNotifier());

        public ReviewContractWorkItemsCommandHandler CreateReviewHandler() =>
            new(Context, new FixedClock(Now), new CapturingUserAuditLogService(), new NoopChatRealtimeNotifier());

        private sealed class FixedClock(DateTime utcNow) : IDateTimeService
        {
            public DateTime UtcNow { get; } = utcNow;
        }
    }
}
