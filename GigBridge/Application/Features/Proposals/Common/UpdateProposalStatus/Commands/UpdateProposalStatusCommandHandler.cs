using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Common;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;
using Application.Features.Proposals.Common;
using Application.Features.Premium.Client.SmartTalentMatching.Feedback;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Common.UpdateProposalStatus.Commands;

public class UpdateProposalStatusCommandHandler
    : IRequestHandler<UpdateProposalStatusCommand, UpdateProposalStatusResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IProposalQuestionTimerService? _proposalQuestionTimerService;
    private readonly IProposalInterviewReviewService? _proposalInterviewReviewService;

    public UpdateProposalStatusCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IProposalQuestionTimerService? proposalQuestionTimerService = null,
        IProposalInterviewReviewService? proposalInterviewReviewService = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _proposalQuestionTimerService = proposalQuestionTimerService;
        _proposalInterviewReviewService = proposalInterviewReviewService;
    }

    public async Task<UpdateProposalStatusResponse> Handle(
        UpdateProposalStatusCommand command,
        CancellationToken cancellationToken)
    {
        var proposal = await _context.Set<Proposal>()
            .Include(proposal => proposal.JobPosts)
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
