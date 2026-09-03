using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
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

        var handler = new UpdateProposalStatusCommandHandler(
            context, new FixedDateTimeService(now), new NoopNotificationService());
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

        var handler = new UpdateProposalStatusCommandHandler(
            context, new FixedDateTimeService(now), new NoopNotificationService());
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
        var clientProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();

        var clientUserId = Guid.NewGuid();
        var clientProfile = new ClientProfile { ClientProfilesId = clientProfileId, UserId = clientUserId };
        var jobPost = new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = "Interview job",
            Description = "Answer questions.",
            Status = 1,
            CreatedAt = now,
            ClientProfiles = clientProfile
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
        context.AddSet(clientProfile);
        context.AddSet<UserEloPointTransaction>();
        context.AddSet<Notification>();
        var notificationService = new SpyNotificationService(context);
        var handler = new UpdateProposalStatusCommandHandler(context, new FixedDateTimeService(now), notificationService);

        var result = await handler.Handle(
            new UpdateProposalStatusCommand(
                proposalId,
                freelancerUserId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, proposal.Status);
        Assert.Empty(context.Set<UserEloPointTransaction>());

        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal(clientUserId, notification.UserId);
        Assert.Equal(NotificationType.ProposalReceived, notification.Type);
        Assert.Equal(jobPostId, notification.ReferenceId);
        Assert.Equal("ProposalMilestone", notification.ReferenceType);
        Assert.Contains("1", notification.Title);
        Assert.Contains(jobPost.Title, notification.Title);
    }

    [Fact]
    public async Task Handle_SubmitDraftWithoutWorkBreakdown_ThrowsBadRequest()
    {
        var (handler, proposal, userId, _, _, _) = CreateDraftSubmissionHandler();
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
        var (handler, proposal, userId, _, _, _) = CreateDraftSubmissionHandler();
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
        var (handler, proposal, userId, _, _, _) = CreateDraftSubmissionHandler();
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
        var (handler, proposal, userId, _, _, _) = CreateDraftSubmissionHandler();
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
        var (handler, proposal, userId, _, _, _) = CreateDraftSubmissionHandler();
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
        var (handler, proposal, userId, _, _, _) = CreateDraftSubmissionHandler();
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
        var (handler, proposal, userId, _, _, _) = CreateDraftSubmissionHandler();
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

        var handler = new UpdateProposalStatusCommandHandler(
            context, new FixedDateTimeService(now), new NoopNotificationService());

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                clientUserId,
                new UpdateProposalStatusRequest { Status = 2 }),
            CancellationToken.None));

        Assert.Contains("no longer open", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, proposal.Status);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    [InlineData(4, 5)]
    [InlineData(9, 10)]
    [InlineData(19, 20)]
    [InlineData(49, 50)]
    [InlineData(99, 100)]
    [InlineData(199, 200)]
    [InlineData(599, 600)]
    public async Task Handle_SubmitDraft_NotifiesClientAtProposalMilestone(
        int existingSubmittedProposals, int expectedProposalCount)
    {
        var notificationService = new SpyNotificationService();
        var (handler, proposal, userId, jobPostId, clientUserId, context) =
            CreateDraftSubmissionHandler(notificationService, existingSubmittedProposals);
        notificationService.Context = context;

        var result = await handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                userId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None);

        Assert.True(result.Success);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal(clientUserId, notification.UserId);
        Assert.Equal(NotificationType.ProposalReceived, notification.Type);
        Assert.Equal(jobPostId, notification.ReferenceId);
        Assert.Equal("ProposalMilestone", notification.ReferenceType);
        Assert.Contains($"Đã có {expectedProposalCount} Proposal", notification.Title);
        Assert.Contains(proposal.JobPosts.Title, notification.Title);
        Assert.NotNull(notification.Metadata);
        using var metadataDoc = JsonDocument.Parse(notification.Metadata!);
        Assert.Equal(expectedProposalCount, metadataDoc.RootElement.GetProperty("proposalCount").GetInt32());
        Assert.Equal(jobPostId, metadataDoc.RootElement.GetProperty("jobPostId").GetGuid());
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(10, 11)]
    [InlineData(20, 21)]
    public async Task Handle_SubmitDraft_DoesNotNotifyOnNonMilestoneCount(
        int existingSubmittedProposals, int expectedProposalCount)
    {
        var notificationService = new SpyNotificationService();
        var (handler, proposal, userId, _, _, context) =
            CreateDraftSubmissionHandler(notificationService, existingSubmittedProposals);
        notificationService.Context = context;

        var result = await handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                userId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expectedProposalCount, existingSubmittedProposals + 1);
        Assert.Empty(notificationService.Notifications);
    }

    [Fact]
    public async Task Handle_SubmitDraft_CountsProposalsPerJobPostOnly()
    {
        var notificationService = new SpyNotificationService();
        var (handler, proposal, userId, jobPostId, clientUserId, context) =
            CreateDraftSubmissionHandler(notificationService, existingSubmittedProposals: 9);
        notificationService.Context = context;

        var otherJobPostId = Guid.NewGuid();
        var otherClientProfile = new ClientProfile { ClientProfilesId = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var otherJobPost = new JobPost
        {
            JobPostsId = otherJobPostId,
            ClientProfilesId = otherClientProfile.ClientProfilesId,
            Title = "Unrelated job",
            Description = "A different job post.",
            Status = 1,
            CreatedAt = proposal.JobPosts.CreatedAt,
            ClientProfiles = otherClientProfile
        };
        var otherProposals = context.Set<Proposal>();
        for (var i = 0; i < 30; i++)
        {
            otherProposals.Add(new Proposal
            {
                ProposalsId = Guid.NewGuid(),
                JobPostsId = otherJobPostId,
                FreelancerProfilesId = Guid.NewGuid(),
                Status = 1,
                JobPosts = otherJobPost
            });
        }

        var result = await handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                userId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None);

        Assert.True(result.Success);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal(clientUserId, notification.UserId);
        Assert.Equal(jobPostId, notification.ReferenceId);
        Assert.Contains("Đã có 10 Proposal", notification.Title);
    }

    [Fact]
    public async Task Handle_SubmitDraft_DoesNotDuplicateNotificationForSameMilestone()
    {
        var notificationService = new SpyNotificationService();
        var (handler, proposal, userId, jobPostId, _, context) =
            CreateDraftSubmissionHandler(notificationService, existingSubmittedProposals: 0);
        notificationService.Context = context;

        context.AddSet(new Notification
        {
            NotificationsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Type = (int)NotificationType.ProposalReceived,
            Title = "Đã có 1 Proposal ứng tuyển vào Project request.",
            ReferenceId = jobPostId,
            ReferenceType = "ProposalMilestone",
            Metadata = System.Text.Json.JsonSerializer.Serialize(new { jobPostId, proposalCount = 1 }),
            IsRead = false,
            CreatedAt = proposal.JobPosts.CreatedAt
        });

        var result = await handler.Handle(
            new UpdateProposalStatusCommand(
                proposal.ProposalsId,
                userId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(notificationService.Notifications);
    }

    private static (
        UpdateProposalStatusCommandHandler Handler,
        Proposal Proposal,
        Guid UserId,
        Guid JobPostId,
        Guid ClientUserId,
        InMemoryApplicationDbContext Context) CreateDraftSubmissionHandler(
        INotificationService? notificationService = null,
        int existingSubmittedProposals = 0)
    {
        var now = new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var clientUserId = Guid.NewGuid();
        var clientProfile = new ClientProfile { ClientProfilesId = clientProfileId, UserId = clientUserId };
        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfileId,
            Title = "Project request",
            Description = "Build the requested product.",
            Status = 1,
            CreatedAt = now,
            ClientProfiles = clientProfile
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
        var proposals = context.AddSet(proposal);
        for (var i = 0; i < existingSubmittedProposals; i++)
        {
            proposals.Add(new Proposal
            {
                ProposalsId = Guid.NewGuid(),
                JobPostsId = jobPost.JobPostsId,
                FreelancerProfilesId = Guid.NewGuid(),
                Status = 1,
                JobPosts = jobPost
            });
        }
        context.AddSet(clientProfile);
        context.AddSet<Notification>();

        var handler = new UpdateProposalStatusCommandHandler(
            context, new FixedDateTimeService(now), notificationService ?? new NoopNotificationService());

        return (handler, proposal, userId, jobPost.JobPostsId, clientUserId, context);
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
        public SpyNotificationService()
        {
        }

        public SpyNotificationService(InMemoryApplicationDbContext context)
        {
            Context = context;
        }

        public InMemoryApplicationDbContext? Context { get; set; }

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
                metadata,
                Context?.SaveChangesCount ?? 0));

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
        string? Metadata,
        int SaveChangesCountAtCreation);
}

