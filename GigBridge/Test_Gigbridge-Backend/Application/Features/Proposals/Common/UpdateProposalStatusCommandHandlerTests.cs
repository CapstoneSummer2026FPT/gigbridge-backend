using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;
using Test_Gigbridge_Backend.TestSupport;


namespace Test_Gigbridge_Backend.Application.Features.Proposals.Common;

public class UpdateProposalStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_AcceptProposal_ThrowsBadRequestBecauseFinalOfferFlowIsRequired()
    {
        var now = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var clientUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var acceptedProposalId = Guid.NewGuid();
        var otherProposalId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        var jobPost = new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = "Fixed price job",
            Description = "Build the fixed price workflow.",
            Status = 1,
            CreatedAt = now
        };

        var draftContract = new Contract
        {
            ContractsId = contractId,
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = jobPost.Title,
            Description = jobPost.Description,
            TotalBudget = 500m,
            Status = (int)ContractStatus.PendingFreelancerSelection,
            CreatedAt = now
        };

        var acceptedProposal = new Proposal
        {
            ProposalsId = acceptedProposalId,
            JobPostsId = jobPostId,
            FreelancerProfilesId = freelancerProfileId,
            ProposedBudget = 1234m,
            Status = 0,
            JobPosts = jobPost
        };

        var otherProposal = new Proposal
        {
            ProposalsId = otherProposalId,
            JobPostsId = jobPostId,
            FreelancerProfilesId = Guid.NewGuid(),
            ProposedBudget = 900m,
            Status = 1,
            JobPosts = jobPost
        };

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = clientUserId });
        context.AddSet(jobPost);
        context.AddSet(acceptedProposal, otherProposal);
        context.AddSet(draftContract);
        var escrows = context.AddSet<ContractEscrow>();

        var handler = new UpdateProposalStatusCommandHandler(context, new FixedDateTimeService(now));
        var command = new UpdateProposalStatusCommand(
            acceptedProposalId,
            clientUserId,
            new UpdateProposalStatusRequest { Status = 3 });

        await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(command, CancellationToken.None));

        Assert.Equal(0, acceptedProposal.Status);
        Assert.Equal(1, otherProposal.Status);
        Assert.Equal(1, jobPost.Status);
        Assert.Null(draftContract.FreelancerProfilesId);
        Assert.Null(draftContract.ProposalsId);
        Assert.Equal(500m, draftContract.TotalBudget);
        Assert.Equal((int)ContractStatus.PendingFreelancerSelection, draftContract.Status);
        Assert.Empty(escrows.Entities);
    }

    [Fact]
    public async Task Handle_AcceptProposalWithoutBudget_ThrowsBadRequestBecauseFinalOfferFlowIsRequired()
    {
        var now = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var clientUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();

        var jobPost = new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = "Fixed price job",
            Description = "Build the fixed price workflow.",
            Status = 1,
            CreatedAt = now
        };

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = clientUserId });
        context.AddSet(jobPost);
        context.AddSet(new Proposal
        {
            ProposalsId = proposalId,
            JobPostsId = jobPostId,
            FreelancerProfilesId = Guid.NewGuid(),
            ProposedBudget = null,
            Status = 0,
            JobPosts = jobPost
        });
        context.AddSet(new Contract
        {
            ContractsId = Guid.NewGuid(),
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = jobPost.Title,
            TotalBudget = 0m,
            Status = (int)ContractStatus.PendingFreelancerSelection,
            CreatedAt = now
        });
        context.AddSet<ContractEscrow>();

        var handler = new UpdateProposalStatusCommandHandler(context, new FixedDateTimeService(now));
        var command = new UpdateProposalStatusCommand(
            proposalId,
            clientUserId,
            new UpdateProposalStatusRequest { Status = 3 });

        await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SubmitDraft_DoesNotApplyRetiredIntegrityPenalty()
    {
        var now = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var freelancerUserId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();

        var jobPost = new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = Guid.NewGuid(),
            Title = "Interview job",
            Description = "Answer questions.",
            Status = 1,
            CreatedAt = now
        };
        var proposal = new Proposal
        {
            ProposalsId = proposalId,
            JobPostsId = jobPostId,
            FreelancerProfilesId = freelancerProfileId,
            CoverLetter = "I will analyze the requirements and deliver the agreed scope with clear milestones.",
            AnalysisSummary = "The project requires a reliable implementation with explicit constraints, risks, and measurable outcomes.",
            SolutionApproach = "I will deliver the solution incrementally, validate each component, and document all important decisions.",
            ProposedBudget = 500m,
            Status = 0,
            JobPosts = jobPost
        };
        var milestone = new ProposalMilestonePlan
        {
            ProposalMilestonePlansId = Guid.NewGuid(),
            ProposalsId = proposalId,
            Title = "Delivery",
            Amount = 500m,
            EstimatedDuration = "1 week",
            DueDate = DateOnly.FromDateTime(now.AddDays(14)),
            Deliverables = "Production-ready implementation",
            AcceptanceCriteria = "All agreed acceptance tests pass"
        };
        proposal.ProposalMilestonePlans.Add(milestone);
        proposal.ProposalWorkBreakdownItems.Add(new ProposalWorkBreakdownItem
        {
            ProposalWorkBreakdownItemsId = Guid.NewGuid(),
            ProposalsId = proposalId,
            ProposalMilestonePlansId = milestone.ProposalMilestonePlansId,
            Title = "Implementation",
            Description = "Implement and verify the agreed project scope."
        });

        context.AddSet(new FreelancerProfile
        {
            FreelancerProfilesId = freelancerProfileId,
            UserId = freelancerUserId
        });
        context.AddSet(jobPost);
        context.AddSet(proposal);
        context.AddSet<ClientProfile>();
        context.AddSet<UserEloPointTransaction>();
        context.AddSet<Notification>();
        var handler = new UpdateProposalStatusCommandHandler(context, new FixedDateTimeService(now));

        var result = await handler.Handle(
            new UpdateProposalStatusCommand(
                proposalId,
                freelancerUserId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, proposal.Status);
        Assert.Empty(context.Set<UserEloPointTransaction>());
        Assert.Empty(context.Set<Notification>());
    }

    [Fact]
    public async Task Handle_SubmitDraftWithoutWorkBreakdown_ThrowsBadRequest()
    {
        var (handler, proposal, userId) = CreateDraftSubmissionHandler();
        proposal.ProposalWorkBreakdownItems.Clear();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                userId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None));

        Assert.Contains("work breakdown", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, proposal.Status);
    }

    [Fact]
    public async Task Handle_SubmitDraftWithBudgetOverride_AllowsMilestoneTotalMismatch()
    {
        var (handler, proposal, userId) = CreateDraftSubmissionHandler();
        proposal.ProposalMilestonePlans.Single().Amount = 400m;

        var result = await handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                userId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, proposal.Status);
        Assert.Equal(500m, proposal.ProposedBudget);
        Assert.Equal(400m, proposal.ProposalMilestonePlans.Single().Amount);
    }

    [Fact]
    public async Task Handle_SubmitDraftWithGeneratedWorkItem_PassesSubmissionGuard()
    {
        var (handler, proposal, userId) = CreateDraftSubmissionHandler();
        var milestone = proposal.ProposalMilestonePlans.Single();
        var workItem = proposal.ProposalWorkBreakdownItems.Single();
        workItem.Title = milestone.Title;
        workItem.Description = milestone.Deliverables;
        workItem.Deliverables = milestone.Deliverables;
        workItem.EstimatedDuration = milestone.EstimatedDuration;

        var result = await handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                userId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, proposal.Status);
        Assert.Equal(milestone.ProposalMilestonePlansId, workItem.ProposalMilestonePlansId);
    }

    [Fact]
    public async Task Handle_WithdrawShortlistedProposal_ThrowsBadRequest()
    {
        var (handler, proposal, userId) = CreateDraftSubmissionHandler();
        proposal.Status = 2;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                userId,
                new UpdateProposalStatusRequest { Status = 5 }),
            CancellationToken.None));

        Assert.Contains("withdraw a pending proposal", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, proposal.Status);
    }

    [Fact]
    public async Task Handle_SubmitDraftWithoutMilestoneDuration_ThrowsBadRequest()
    {
        var (handler, proposal, userId) = CreateDraftSubmissionHandler();
        proposal.ProposalMilestonePlans.Single().EstimatedDuration = null;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                userId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None));

        Assert.Contains("duration", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, proposal.Status);
    }

    [Fact]
    public async Task Handle_SubmitDraftWithoutMilestoneDeadline_ThrowsBadRequest()
    {
        var (handler, proposal, userId) = CreateDraftSubmissionHandler();
        proposal.ProposalMilestonePlans.Single().DueDate = null;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                userId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None));

        Assert.Contains("deadline", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, proposal.Status);
    }

    [Fact]
    public async Task Handle_SubmitDraftWhenJobPostClosed_ThrowsBadRequest()
    {
        var (handler, proposal, userId) = CreateDraftSubmissionHandler();
        proposal.JobPosts.Status = 2;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                userId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None));

        Assert.Contains("not accepting proposals", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, proposal.Status);
    }

    [Fact]
    public async Task Handle_ClientShortlistWhenJobPostClosed_ThrowsBadRequest()
    {
        var now = new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var clientUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfileId,
            Title = "Closed project request",
            Description = "This job is no longer open.",
            Status = 2,
            CreatedAt = now
        };
        var proposal = new Proposal
        {
            ProposalsId = Guid.NewGuid(),
            JobPostsId = jobPost.JobPostsId,
            FreelancerProfilesId = Guid.NewGuid(),
            ProposedBudget = 500m,
            Status = 1,
            JobPosts = jobPost
        };

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = clientUserId });
        context.AddSet(jobPost);
        context.AddSet(proposal);

        var handler = new UpdateProposalStatusCommandHandler(context, new FixedDateTimeService(now));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                clientUserId,
                new UpdateProposalStatusRequest { Status = 2 }),
            CancellationToken.None));

        Assert.Contains("no longer open", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, proposal.Status);
    }

    private static (UpdateProposalStatusCommandHandler Handler, Proposal Proposal, Guid UserId) CreateDraftSubmissionHandler()
    {
        var now = new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = Guid.NewGuid(),
            Title = "Project request",
            Description = "Build the requested product.",
            Status = 1,
            CreatedAt = now
        };
        var proposal = new Proposal
        {
            ProposalsId = Guid.NewGuid(),
            JobPostsId = jobPost.JobPostsId,
            FreelancerProfilesId = freelancerProfileId,
            CoverLetter = "I will analyze the requirements and deliver the agreed scope with clear milestones.",
            AnalysisSummary = "The project requires a reliable implementation with explicit constraints, risks, and measurable outcomes.",
            SolutionApproach = "I will deliver the solution incrementally, validate each component, and document all important decisions.",
            ProposedBudget = 500m,
            Status = 0,
            JobPosts = jobPost
        };
        var milestone = new ProposalMilestonePlan
        {
            ProposalMilestonePlansId = Guid.NewGuid(),
            ProposalsId = proposal.ProposalsId,
            Title = "Delivery",
            Amount = 500m,
            EstimatedDuration = "1 week",
            DueDate = DateOnly.FromDateTime(now.AddDays(14)),
            Deliverables = "Production-ready implementation",
            AcceptanceCriteria = "All agreed acceptance tests pass"
        };
        proposal.ProposalMilestonePlans.Add(milestone);
        proposal.ProposalWorkBreakdownItems.Add(new ProposalWorkBreakdownItem
        {
            ProposalWorkBreakdownItemsId = Guid.NewGuid(),
            ProposalsId = proposal.ProposalsId,
            ProposalMilestonePlansId = milestone.ProposalMilestonePlansId,
            Title = "Implementation",
            Description = "Implement and verify the agreed project scope."
        });

        context.AddSet(new FreelancerProfile
        {
            FreelancerProfilesId = freelancerProfileId,
            UserId = userId
        });
        context.AddSet(jobPost);
        context.AddSet(proposal);
        context.AddSet<ClientProfile>();

        return (new UpdateProposalStatusCommandHandler(context, new FixedDateTimeService(now)), proposal, userId);
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class SpyNotificationService : INotificationService
    {
        private readonly InMemoryApplicationDbContext _context;

        public SpyNotificationService(InMemoryApplicationDbContext context)
        {
            _context = context;
        }

        public List<NotificationCall> Notifications { get; } = new();

        public Task CreateNotificationAsync(
            Guid userId,
            NotificationType type,
            string title,
            string? content = null,
            Guid? referenceId = null,
            string? referenceType = null,
            CancellationToken cancellationToken = default,
            string? metadata = null)
        {
            Notifications.Add(new NotificationCall(
                userId,
                type,
                title,
                content ?? string.Empty,
                referenceId,
                referenceType,
                _context.SaveChangesCount));

            return Task.CompletedTask;
        }

        public Task CreateBroadcastNotificationAsync(
            NotificationTarget target,
            NotificationType type,
            string title,
            string? content = null,
            Guid? referenceId = null,
            string? referenceType = null,
            Guid? targetUserId = null,
            bool sendEmail = false,
            Guid? createdByAdminId = null,
            DateTime? expiresAt = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    

    private sealed record NotificationCall(
        Guid UserId,
        NotificationType Type,
        string Title,
        string Content,
        Guid? ReferenceId,
        string? ReferenceType,
        int SaveChangesCountAtCreation);
}

