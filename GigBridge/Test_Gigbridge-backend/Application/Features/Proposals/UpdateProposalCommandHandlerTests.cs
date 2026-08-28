using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Features.Proposals.Common.DTOs;
using Application.Features.Proposals.Freelancer.UpdateProposal.Commands;
using Application.Features.Proposals.Freelancer.UpdateProposal.DTOs;
using Domain.Entities;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Proposals;

public class UpdateProposalCommandHandlerTests
{
    [Fact]
    public async Task Handle_SecondEditWithExistingMilestonesAndWorkItems_ReplacesPlanWithoutConcurrencyConflict()
    {
        var fixture = new UpdateProposalFixture();
        var handler = new UpdateProposalCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        var request = new UpdateProposalRequest
        {
            CoverLetter = "Updated cover letter for the second edit pass.",
            ProposedDuration = "4 weeks",
            MilestonePlans =
            [
                new ProposalMilestonePlanDto
                {
                    Title = "Revised milestone",
                    Amount = 500m,
                    EstimatedDuration = "2 weeks",
                    Deliverables = "Revised deliverable",
                    OrderIndex = 0,
                    WorkItems =
                    [
                        new ProposalWorkBreakdownItemDto
                        {
                            Title = "Revised work item",
                            Description = "Revised description",
                            MilestoneOrderIndex = 0,
                            OrderIndex = 0
                        }
                    ]
                }
            ]
        };

        var result = await handler.Handle(
            new UpdateProposalCommand(fixture.ProposalId, fixture.FreelancerUserId, request),
            CancellationToken.None);

        Assert.True(result);

        var remainingMilestones = fixture.MilestonePlans.Entities;
        var remainingWorkItems = fixture.WorkItems.Entities;
        Assert.Single(remainingMilestones);
        Assert.Single(remainingWorkItems);
        Assert.Equal("Revised milestone", remainingMilestones[0].Title);
        Assert.Equal("Revised work item", remainingWorkItems[0].Title);
        Assert.Equal(remainingMilestones[0].ProposalMilestonePlansId, remainingWorkItems[0].ProposalMilestonePlansId);
        Assert.DoesNotContain(remainingMilestones, item => item.ProposalMilestonePlansId == fixture.ExistingMilestoneId);
        Assert.DoesNotContain(remainingWorkItems, item => item.ProposalWorkBreakdownItemsId == fixture.ExistingWorkItemId);

        Assert.Equal(1, fixture.Context.TransactionBeginCount);
        Assert.Equal(1, fixture.Context.TransactionCommitCount);
        Assert.Equal(1, fixture.Context.TransactionLockCount);
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class UpdateProposalFixture
    {
        public UpdateProposalFixture()
        {
            Context.AddSet(new FreelancerProfile
            {
                FreelancerProfilesId = FreelancerProfileId,
                UserId = FreelancerUserId
            });

            var jobPost = new JobPost
            {
                JobPostsId = JobPostId,
                ClientProfilesId = Guid.NewGuid(),
                Title = "Sample job",
                Description = "Sample description",
                EndDate = Now.AddDays(30)
            };
            Context.AddSet(jobPost);

            var existingMilestone = new ProposalMilestonePlan
            {
                ProposalMilestonePlansId = ExistingMilestoneId,
                ProposalsId = ProposalId,
                Title = "Original milestone",
                Amount = 400m,
                EstimatedDuration = "1 week",
                OrderIndex = 0
            };
            var existingWorkItem = new ProposalWorkBreakdownItem
            {
                ProposalWorkBreakdownItemsId = ExistingWorkItemId,
                ProposalsId = ProposalId,
                ProposalMilestonePlansId = ExistingMilestoneId,
                Title = "Original work item",
                OrderIndex = 0
            };

            var proposal = new Proposal
            {
                ProposalsId = ProposalId,
                JobPostsId = JobPostId,
                FreelancerProfilesId = FreelancerProfileId,
                Status = 0,
                ModerationStatus = 0,
                JobPosts = jobPost,
                ProposalMilestonePlans = [existingMilestone],
                ProposalWorkBreakdownItems = [existingWorkItem]
            };
            Context.AddSet(proposal);

            MilestonePlans = Context.AddSet(existingMilestone);
            WorkItems = Context.AddSet(existingWorkItem);
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public TestDbSet<ProposalMilestonePlan> MilestonePlans { get; }
        public TestDbSet<ProposalWorkBreakdownItem> WorkItems { get; }

        public DateTime Now { get; } = DateTime.UtcNow;
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid FreelancerProfileId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid ProposalId { get; } = Guid.NewGuid();
        public Guid ExistingMilestoneId { get; } = Guid.NewGuid();
        public Guid ExistingWorkItemId { get; } = Guid.NewGuid();
    }
}
