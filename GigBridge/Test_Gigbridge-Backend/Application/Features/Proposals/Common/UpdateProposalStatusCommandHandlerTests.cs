using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;
using Application.Features.Proposals.Freelancer.Cheating.DTOs;
using Domain.Entities;
using Domain.Enums;
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
    public async Task Handle_SubmitDraftWithNewCheatingViolation_NotifiesFreelancerAfterSaving()
    {
        var now = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var freelancerUserId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var violationId = Guid.NewGuid();

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
            Status = 0,
            JobPosts = jobPost
        };

        context.AddSet(new FreelancerProfile
        {
            FreelancerProfilesId = freelancerProfileId,
            UserId = freelancerUserId
        });
        context.AddSet(jobPost);
        context.AddSet(proposal);
        context.AddSet<ClientProfile>();

        var cheatingPenalty = new CheatingPenaltyResultDto(
            true,
            true,
            violationId,
            3,
            -50,
            (int)CheatingViolationAction.TemporarySuspension,
            now.AddDays(7),
            "Anti-cheat suspension applied: violation 3. Your account is suspended for 7 days. 50 Elo points deducted.");
        var notificationService = new SpyNotificationService(context);
        var handler = new UpdateProposalStatusCommandHandler(
            context,
            new FixedDateTimeService(now),
            new StubProposalCheatingService(cheatingPenalty),
            notificationService: notificationService);

        var result = await handler.Handle(
            new UpdateProposalStatusCommand(
                proposalId,
                freelancerUserId,
                new UpdateProposalStatusRequest { Status = 1 }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, proposal.Status);
        Assert.Single(notificationService.Notifications);
        Assert.Equal(1, notificationService.Notifications[0].SaveChangesCountAtCreation);
        Assert.Equal("Anti-cheat suspension applied", notificationService.Notifications[0].Title);
        Assert.Contains("violation 3", notificationService.Notifications[0].Content);
        Assert.Contains("suspended for 7 days", notificationService.Notifications[0].Content);
        Assert.Equal(violationId, notificationService.Notifications[0].ReferenceId);
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class StubProposalCheatingService : IProposalCheatingService
    {
        private readonly CheatingPenaltyResultDto? _penaltyResult;

        public StubProposalCheatingService(CheatingPenaltyResultDto? penaltyResult)
        {
            _penaltyResult = penaltyResult;
        }

        public Task<CheatingEventLogResponse> LogEventAsync(
            Guid proposalId,
            Guid freelancerUserId,
            LogProposalCheatingEventRequest request,
            string? ipAddress,
            string? userAgent,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<CheatingPenaltyResultDto?> ApplySubmissionPenaltyIfNeededAsync(
            Proposal proposal,
            Guid freelancerUserId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_penaltyResult);
        }
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
            CancellationToken cancellationToken = default)
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
