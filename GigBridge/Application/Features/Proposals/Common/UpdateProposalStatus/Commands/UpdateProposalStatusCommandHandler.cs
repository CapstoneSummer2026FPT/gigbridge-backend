using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Common.InternalServices.Proposals.Interfaces;
using Application.Features.JobPosts.Common;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;
using Application.Features.Proposals.Common;
using Application.Features.Premium.Client.SmartTalentMatching.Feedback;
using Domain.Entities;
using Domain.Enums.Notifications;
using Domain.Enums.Premium;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.Proposals.Common.UpdateProposalStatus.Commands;

public class UpdateProposalStatusCommandHandler
    : IRequestHandler<UpdateProposalStatusCommand, UpdateProposalStatusResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly IProposalQuestionTimerService? _proposalQuestionTimerService;
    private readonly IProposalInterviewReviewService? _proposalInterviewReviewService;

    public UpdateProposalStatusCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        IProposalQuestionTimerService? proposalQuestionTimerService = null,
        IProposalInterviewReviewService? proposalInterviewReviewService = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _proposalQuestionTimerService = proposalQuestionTimerService;
        _proposalInterviewReviewService = proposalInterviewReviewService;
    }

    public async Task<UpdateProposalStatusResponse> Handle(
        UpdateProposalStatusCommand command,
        CancellationToken cancellationToken)
    {
        var proposal = await _context.Set<Proposal>()
            .Include(proposal => proposal.JobPosts).ThenInclude(jobPost => jobPost.ClientProfiles)
            .Include(proposal => proposal.ProposalWorkBreakdownItems)
            .Include(proposal => proposal.ProposalMilestonePlans)
            .FirstOrDefaultAsync(
                proposal => proposal.ProposalsId == command.ProposalId,
                cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        ProposalModerationGuard.EnsureActive(proposal);

        if (proposal.Status == 3 || proposal.Status == 4 || proposal.Status == 5)
        {
            throw new BadRequestException("Only draft, pending or shortlisted proposal can be updated.");
        }

        var requestedStatus = command.Request.Status;

        var isClientOwner = await _context.Set<ClientProfile>()
            .AnyAsync(
                clientProfile =>
                    clientProfile.UserId == command.UserId &&
                    clientProfile.ClientProfilesId == proposal.JobPosts.ClientProfilesId,
                cancellationToken);

        var isFreelancerOwner = await _context.Set<FreelancerProfile>()
            .AnyAsync(
                freelancerProfile =>
                    freelancerProfile.UserId == command.UserId &&
                    freelancerProfile.FreelancerProfilesId == proposal.FreelancerProfilesId,
                cancellationToken);

        if (isClientOwner)
        {
            JobPostNegotiationGuard.EnsureEligibleForNegotiation(proposal.JobPosts);
            UpdateStatusByClient(proposal, requestedStatus);
        }
        else if (isFreelancerOwner)
        {
            var isDraftSubmission = proposal.Status == 0 && requestedStatus == 1;
            if (isDraftSubmission)
            {
                EnsureJobPostAcceptsProposalSubmission(proposal.JobPosts);
                ProposalSubmissionGuard.EnsureCanSubmit(
                    proposal,
                    DateOnly.FromDateTime(_dateTimeService.UtcNow));
                proposal.SubmittedAt = _dateTimeService.UtcNow;
            }
            if (isDraftSubmission && _proposalQuestionTimerService is not null)
            {
                await _proposalQuestionTimerService.EnsureProposalReadyForSubmissionAsync(
                    proposal,
                    command.UserId,
                    cancellationToken);
            }

            if (isDraftSubmission && _proposalInterviewReviewService is not null)
            {
                await _proposalInterviewReviewService.CompleteActiveReviewForSubmissionAsync(
                    proposal,
                    command.UserId,
                    cancellationToken);
            }

            UpdateStatusByFreelancer(proposal, requestedStatus);
            if (isDraftSubmission)
            {
                await TalentMatchFeedbackWriter.TryAddLatestAttributedAsync(
                    _context,
                    proposal.JobPostsId,
                    proposal.FreelancerProfilesId,
                    TalentMatchEventType.ProposalSubmitted,
                    proposal.ProposalsId,
                    _dateTimeService.UtcNow,
                    cancellationToken);
            }
            proposal.UpdatedAt = _dateTimeService.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            if (isDraftSubmission)
            {
                await NotifyClientOfProposalMilestoneAsync(proposal, cancellationToken);
            }

            return new UpdateProposalStatusResponse(true, proposal.Status);
        }
        else
        {
            throw new UnauthorizedAccessException("You do not have permission to update this proposal.");
        }

        proposal.UpdatedAt = _dateTimeService.UtcNow;

        if (requestedStatus == 2)
        {
            await TalentMatchFeedbackWriter.TryAddLatestAttributedAsync(
                _context,
                proposal.JobPostsId,
                proposal.FreelancerProfilesId,
                TalentMatchEventType.Shortlisted,
                proposal.ProposalsId,
                _dateTimeService.UtcNow,
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateProposalStatusResponse(true, proposal.Status);
    }

    private const string ProposalMilestoneReferenceType = "ProposalMilestone";

    private static bool IsProposalMilestone(int proposalCount) =>
        proposalCount is 1 or 3 or 5 or 10 or 20 or 50
        || (proposalCount >= 100 && proposalCount % 100 == 0);

    private async Task NotifyClientOfProposalMilestoneAsync(
        Proposal proposal,
        CancellationToken cancellationToken)
    {
        var proposalCount = await _context.Set<Proposal>()
            .CountAsync(
                p => p.JobPostsId == proposal.JobPostsId && p.Status != 0,
                cancellationToken);

        if (!IsProposalMilestone(proposalCount))
        {
            return;
        }

        var jobPostId = proposal.JobPostsId;

        var existingMetadata = await _context.Set<Notification>()
            .Where(n => n.ReferenceId == jobPostId && n.ReferenceType == ProposalMilestoneReferenceType)
            .Select(n => n.Metadata)
            .ToListAsync(cancellationToken);

        var alreadyNotified = existingMetadata.Any(metadata =>
        {
            if (string.IsNullOrEmpty(metadata))
            {
                return false;
            }

            using var document = JsonDocument.Parse(metadata);
            return document.RootElement.TryGetProperty("proposalCount", out var count)
                && count.GetInt32() == proposalCount;
        });

        if (alreadyNotified)
        {
            return;
        }

        var message = $"Đã có {proposalCount} Proposal ứng tuyển vào {proposal.JobPosts.Title}.";
        var metadataJson = JsonSerializer.Serialize(new { jobPostId, proposalCount });

        await _notificationService.CreateNotificationAsync(
            proposal.JobPosts.ClientProfiles.UserId,
            NotificationType.ProposalReceived,
            message,
            message,
            jobPostId,
            ProposalMilestoneReferenceType,
            cancellationToken,
            metadataJson);
    }

    private void EnsureJobPostAcceptsProposalSubmission(JobPost jobPost)
    {
        if (jobPost.Visibility == JobPostNegotiationGuard.AdminLockedVisibility)
        {
            throw new BadRequestException("This job post has been locked by an admin and is not accepting proposals.");
        }

        if (jobPost.Status != JobPostNegotiationGuard.OpenStatus)
        {
            throw new BadRequestException("This job post is not accepting proposals.");
        }

        if (jobPost.EndDate.HasValue && jobPost.EndDate.Value <= _dateTimeService.UtcNow)
        {
            throw new BadRequestException("This job post application deadline has passed.");
        }
    }

    private void UpdateStatusByClient(
    Proposal proposal,
    int requestedStatus)
    {
        if (proposal.Status == 0)
        {
            throw new BadRequestException("Client cannot update draft proposal.");
        }
        // 2 = Shortlisted, 4 = Rejected. Accepting must go through the final-offer flow.
        if (requestedStatus != 2 && requestedStatus != 4)
        {
            throw new BadRequestException(
                "Client can only update proposal status to Shortlisted or Rejected. Use the final-offer flow to accept a proposal.");
        }

        proposal.Status = requestedStatus;
    }


    private static void UpdateStatusByFreelancer(
    Proposal proposal,
    int requestedStatus)
    {
        // Draft -> Pending: Freelancer submit draft proposal
        if (proposal.Status == 0 && requestedStatus == 1)
        {
            proposal.Status = 1;
            return;
        }

        // Pending -> Withdrawn: once the client has shortlisted/accepted the proposal for negotiation,
        // the freelancer can no longer withdraw it from the active hiring flow.
        if (proposal.Status == 1 && requestedStatus == 5)
        {
            proposal.Status = 5;
            return;
        }

        throw new BadRequestException(
            "Freelancer can only submit a draft proposal or withdraw a pending proposal.");
    }
}
