using Application.Common.Interfaces.Time;
using Application.Features.Contracts.Milestones.WorkItems.Client.Review.Commands;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.Delivery;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Milestones.WorkItems;

/// <summary>
/// The client reviews work items in whatever order they like, over whatever subset they like. Only
/// the approval that leaves nothing outstanding closes the milestone and opens the next one.
///
/// The ApprovedAt assertion is not cosmetic: ContractAutoCompletionWorker selects on
/// <c>!milestone.ApprovedAt.HasValue</c>, so a milestone approved without that stamp would leave the
/// contract unable to auto-complete, with nothing visibly wrong.
/// </summary>
public sealed class ApproveContractWorkItemsBulkTests
{
    [Fact]
    public async Task ApprovingOnlySomeWorkItems_LeavesTheMilestoneOpenAndSendsNoEmail()
    {
        var fixture = new BulkReviewFixture();

        var result = await fixture.CreateHandler().Handle(
            new ReviewContractWorkItemsCommand(
                fixture.ContractId, fixture.MilestoneId, fixture.ClientUserId,
                [fixture.SecondWorkItemId], Approve: true, Reason: null),
            CancellationToken.None);

        Assert.False(result.MilestoneCompleted);
        Assert.Equal((int)MilestoneStatus.Submitted, fixture.Milestone.Status);
        Assert.Null(fixture.Milestone.ApprovedAt);
        Assert.Equal((int)MilestoneStatus.Pending, fixture.NextMilestone.Status);

        // Partial approval is deliberately silent: one email per box ticked is exactly the noise
        // that makes people mute notifications.
        Assert.Empty(fixture.Outbox.Entities);
    }

    [Fact]
    public async Task ApprovingTheLastWorkItem_ClosesTheMilestoneStampsApprovedAtAndOpensTheNextOne()
    {
        var fixture = new BulkReviewFixture();
        var notifier = new CapturingChatRealtimeNotifier();
        var handler = fixture.CreateHandler(notifier);

        // Reviewed out of order on purpose — submission and review are explicitly not a queue.
        await handler.Handle(
            new ReviewContractWorkItemsCommand(
                fixture.ContractId, fixture.MilestoneId, fixture.ClientUserId,
                [fixture.SecondWorkItemId], Approve: true, Reason: null),
            CancellationToken.None);

        var result = await handler.Handle(
            new ReviewContractWorkItemsCommand(
                fixture.ContractId, fixture.MilestoneId, fixture.ClientUserId,
                [fixture.FirstWorkItemId], Approve: true, Reason: null),
            CancellationToken.None);

        Assert.True(result.MilestoneCompleted);
        Assert.Equal((int)MilestoneStatus.Approved, fixture.Milestone.Status);
        Assert.NotNull(fixture.Milestone.ApprovedAt);

        Assert.Equal((int)MilestoneStatus.InProgress, fixture.NextMilestone.Status);
        Assert.Equal(fixture.NextMilestoneId, result.NextMilestoneId);
        Assert.Equal(fixture.NextMilestone.Title, result.NextMilestoneTitle);

        Assert.Single(notifier.UsersEvents.Where(e => e.EventName == "MilestoneAutoCompleted"));
    }

    [Fact]
    public async Task ApprovingTheLastWorkItem_CancelsPendingEarlyStartRequestForTheOpenedMilestone()
    {
        var fixture = new BulkReviewFixture();
        var earlyStartRequest = fixture.AddPendingEarlyStartRequest();

        await fixture.CreateHandler().Handle(
            new ReviewContractWorkItemsCommand(
                fixture.ContractId, fixture.MilestoneId, fixture.ClientUserId,
                [fixture.FirstWorkItemId, fixture.SecondWorkItemId], Approve: true, Reason: null),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.InProgress, fixture.NextMilestone.Status);
        Assert.Equal((int)MilestoneEarlyStartRequestStatus.Cancelled, earlyStartRequest.Status);
        Assert.Equal(fixture.Now, earlyStartRequest.RespondedAt);
        Assert.Null(earlyStartRequest.RespondedByUserId);
        Assert.Equal(
            "Automatically cancelled because the milestone started through the normal workflow.",
            earlyStartRequest.ResponseNote);
    }

    [Fact]
    public async Task ApprovingAnAlreadyApprovedWorkItem_ChangesNothingAndAnnouncesNoSecondCompletion()
    {
        var fixture = new BulkReviewFixture();
        var notifier = new CapturingChatRealtimeNotifier();
        var handler = fixture.CreateHandler(notifier);

        var command = new ReviewContractWorkItemsCommand(
            fixture.ContractId, fixture.MilestoneId, fixture.ClientUserId,
            [fixture.FirstWorkItemId, fixture.SecondWorkItemId], Approve: true, Reason: null);

        await handler.Handle(command, CancellationToken.None);
        var approvedAt = fixture.Milestone.ApprovedAt;

        var repeat = await handler.Handle(command, CancellationToken.None);

        Assert.False(repeat.MilestoneCompleted);
        Assert.Equal(approvedAt, fixture.Milestone.ApprovedAt);
        Assert.Single(notifier.UsersEvents.Where(e => e.EventName == "MilestoneAutoCompleted"));
    }

    [Fact]
    public async Task RequestingRevision_ReopensTheMilestoneAndOnlyTouchesTheNamedWorkItems()
    {
        var fixture = new BulkReviewFixture();
        fixture.Milestone.Status = (int)MilestoneStatus.Submitted;
        fixture.Milestone.SubmittedAt = fixture.Now;

        await fixture.CreateHandler().Handle(
            new ReviewContractWorkItemsCommand(
                fixture.ContractId, fixture.MilestoneId, fixture.ClientUserId,
                [fixture.FirstWorkItemId], Approve: false, Reason: "Needs the source files too."),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.InProgress, fixture.Milestone.Status);
        Assert.Null(fixture.Milestone.SubmittedAt);
        Assert.Equal((int)ContractWorkItemStatus.RevisionRequired, fixture.FirstWorkItem.Status);
        Assert.Equal((int)ContractWorkItemStatus.Submitted, fixture.SecondWorkItem.Status);

        var outbox = Assert.Single(fixture.Outbox.Entities);
        Assert.Equal((int)DeliveryOutboxType.WorkItemRevisionRequested, outbox.DeliveryType);
    }

    [Fact]
    public async Task RequestingRevision_RecordsTheReasonOnTheLatestAttemptOnly()
    {
        var fixture = new BulkReviewFixture();

        await fixture.CreateHandler().Handle(
            new ReviewContractWorkItemsCommand(
                fixture.ContractId, fixture.MilestoneId, fixture.ClientUserId,
                [fixture.FirstWorkItemId], Approve: false, Reason: "Missing the export."),
            CancellationToken.None);

        var attempts = fixture.Attempts.Entities
            .Where(submission => submission.ContractWorkItemId == fixture.FirstWorkItemId)
            .OrderBy(submission => submission.RevisionNumber)
            .ToList();

        // A review decides the open attempt; it never creates one. The next revision row appears
        // only when the freelancer resubmits, which is what keeps the history append-only.
        var latest = Assert.Single(attempts);
        Assert.Equal((int)ContractWorkItemSubmissionReviewStatus.RevisionRequired, latest.ReviewStatus);
        Assert.Equal("Missing the export.", latest.ReviewReason);
        Assert.Equal(fixture.ClientUserId, latest.ReviewedByUserId);
        Assert.NotNull(latest.ReviewedAt);
    }

    private sealed class BulkReviewFixture
    {
        public BulkReviewFixture()
        {
            Contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = FreelancerProfileId,
                Title = "Bulk review contract",
                TotalBudget = 2_000m,
                Status = (int)ContractStatus.Active,
                DeliveryMode = (int)MilestoneDeliveryMode.WorkItem,
                CreatedAt = Now
            };

            Milestone = new Milestone
            {
                MilestonesId = MilestoneId,
                ContractsId = ContractId,
                Title = "Milestone 1",
                Amount = 1_000m,
                Status = (int)MilestoneStatus.Submitted,
                SubmittedAt = Now,
                SortOrder = 0,
                CreatedAt = Now
            };

            NextMilestone = new Milestone
            {
                MilestonesId = NextMilestoneId,
                ContractsId = ContractId,
                Title = "Milestone 2",
                Amount = 1_000m,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 1,
                CreatedAt = Now
            };

            FirstWorkItem = NewWorkItem(FirstWorkItemId, "Work item 1", 0);
            SecondWorkItem = NewWorkItem(SecondWorkItemId, "Work item 2", 1);

            Context.AddSet(
                new User { UserId = ClientUserId, Role = (int)UserRole.Client, Email = "client@example.com", FullName = "Client" },
                new User { UserId = FreelancerUserId, Role = (int)UserRole.Freelancer, Email = "freelancer@example.com", FullName = "Freelancer" });
            Context.AddSet(new ClientProfile { ClientProfilesId = ClientProfileId, UserId = ClientUserId });
            Context.AddSet(new FreelancerProfile { FreelancerProfilesId = FreelancerProfileId, UserId = FreelancerUserId });
            Context.AddSet(Contract);
            Context.AddSet(Milestone, NextMilestone);
            Context.AddSet(FirstWorkItem, SecondWorkItem);

            // One open attempt each: that is the row a review decides.
            Attempts = Context.AddSet(
                NewAttempt(FirstWorkItemId),
                NewAttempt(SecondWorkItemId));

            // Captured, because AddSet<T>() replaces the set rather than returning the existing one.
            Outbox = Context.AddSet<DeliveryOutbox>();
        }

        private ContractWorkItem NewWorkItem(Guid id, string title, int orderIndex) => new()
        {
            ContractWorkItemId = id,
            MilestonesId = MilestoneId,
            Title = title,
            OrderIndex = orderIndex,
            Status = (int)ContractWorkItemStatus.Submitted,
            CreatedAt = Now
        };

        private ContractWorkItemSubmission NewAttempt(Guid workItemId) => new()
        {
            ContractWorkItemSubmissionId = Guid.NewGuid(),
            ContractWorkItemId = workItemId,
            RevisionNumber = 1,
            SubmissionBatchId = Guid.NewGuid(),
            SubmittedAt = Now,
            SubmittedByUserId = FreelancerUserId,
            ReviewStatus = (int)ContractWorkItemSubmissionReviewStatus.Submitted
        };

        public InMemoryApplicationDbContext Context { get; } = new();
        public TestDbSet<ContractWorkItemSubmission> Attempts { get; }
        public TestDbSet<DeliveryOutbox> Outbox { get; }
        public DateTime Now { get; } = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerProfileId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Guid MilestoneId { get; } = Guid.NewGuid();
        public Guid NextMilestoneId { get; } = Guid.NewGuid();
        public Guid FirstWorkItemId { get; } = Guid.NewGuid();
        public Guid SecondWorkItemId { get; } = Guid.NewGuid();
        public Contract Contract { get; }
        public Milestone Milestone { get; }
        public Milestone NextMilestone { get; }
        public ContractWorkItem FirstWorkItem { get; }
        public ContractWorkItem SecondWorkItem { get; }

        public MilestoneEarlyStartRequest AddPendingEarlyStartRequest()
        {
            var request = new MilestoneEarlyStartRequest
            {
                MilestoneEarlyStartRequestId = Guid.NewGuid(),
                ContractsId = ContractId,
                MilestonesId = NextMilestoneId,
                RequestedByUserId = FreelancerUserId,
                Reason = "Start milestone 2 early.",
                Status = (int)MilestoneEarlyStartRequestStatus.Pending,
                CreatedAt = Now.AddMinutes(-5)
            };
            Context.Set<MilestoneEarlyStartRequest>().Add(request);
            return request;
        }

        public ReviewContractWorkItemsCommandHandler CreateHandler(CapturingChatRealtimeNotifier? notifier = null) =>
            new(
                Context,
                new FixedClock(Now),
                new CapturingUserAuditLogService(),
                notifier ?? new CapturingChatRealtimeNotifier());

        private sealed class FixedClock(DateTime utcNow) : IDateTimeService
        {
            public DateTime UtcNow { get; } = utcNow;
        }
    }
}
