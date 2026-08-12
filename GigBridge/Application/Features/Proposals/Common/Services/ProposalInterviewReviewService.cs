using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Proposals.Common.Interfaces;
using Application.Features.Proposals.Common.Services;
using Application.Features.Proposals.Common;
using Application.Features.Proposals.Freelancer.InterviewReview.DTOs;
using Domain.Entities;
using Domain.Enums.Proposals;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Common.Services;

public class ProposalInterviewReviewService : IProposalInterviewReviewService
{
    private const int ReviewSecondsPerQuestion = 60;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public ProposalInterviewReviewService(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<InterviewReviewSessionDto> StartReviewAsync(
        Guid proposalId,
        Guid freelancerUserId,
        CancellationToken cancellationToken)
    {
        var proposal = await GetOwnedDraftProposalAsync(proposalId, freelancerUserId, cancellationToken);
        await EnsureInterviewCompletedAsync(proposal, freelancerUserId, cancellationToken);

        var reviewableQuestionIds = await GetReviewableQuestionIdsAsync(proposal, cancellationToken);
        var session = await _context.Set<ProposalInterviewReviewSession>()
            .FirstOrDefaultAsync(
                existingSession => existingSession.ProposalsId == proposalId,
                cancellationToken);

        var now = _dateTimeService.UtcNow;
        if (session is null)
        {
            var reviewSeconds = reviewableQuestionIds.Count * ReviewSecondsPerQuestion;
            session = new ProposalInterviewReviewSession
            {
                ProposalInterviewReviewSessionsId = Guid.NewGuid(),
                ProposalsId = proposalId,
                FreelancerUserId = freelancerUserId,
                StartedAt = now,
                ExpiresAt = now.AddSeconds(reviewSeconds),
                CompletedAt = reviewSeconds == 0 ? now : null,
                IsLocked = reviewSeconds == 0,
                ReviewableQuestionCount = reviewableQuestionIds.Count,
                CreatedAt = now
            };

            _context.Set<ProposalInterviewReviewSession>().Add(session);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            EnsureSessionOwner(session, freelancerUserId);
            await LockExpiredSessionAsync(session, now, cancellationToken);
        }

        return ToDto(session, reviewableQuestionIds, now);
    }

    public async Task<InterviewReviewSessionDto> CompleteReviewAsync(
        Guid proposalId,
        Guid freelancerUserId,
        CancellationToken cancellationToken)
    {
        var proposal = await GetOwnedDraftProposalAsync(proposalId, freelancerUserId, cancellationToken);
        var session = await GetSessionAsync(proposal.ProposalsId, freelancerUserId, cancellationToken);
        var now = _dateTimeService.UtcNow;

        if (!session.IsLocked)
        {
            session.IsLocked = true;
            session.CompletedAt = now;
            session.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
        }

        var reviewableQuestionIds = await GetReviewableQuestionIdsAsync(proposal, cancellationToken);
        return ToDto(session, reviewableQuestionIds, now);
    }

    public async Task CompleteActiveReviewForSubmissionAsync(
        Proposal proposal,
        Guid freelancerUserId,
        CancellationToken cancellationToken)
    {
        var session = await _context.Set<ProposalInterviewReviewSession>()
            .FirstOrDefaultAsync(
                existingSession => existingSession.ProposalsId == proposal.ProposalsId,
                cancellationToken);

        if (session is null)
        {
            return;
        }

        EnsureSessionOwner(session, freelancerUserId);
        if (session.IsLocked)
        {
            return;
        }

        var now = _dateTimeService.UtcNow;
        session.IsLocked = true;
        session.CompletedAt = now;
        session.UpdatedAt = now;
    }

    private async Task<Proposal> GetOwnedDraftProposalAsync(
        Guid proposalId,
        Guid freelancerUserId,
        CancellationToken cancellationToken)
    {
        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == freelancerUserId, cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var proposal = await _context.Set<Proposal>()
            .FirstOrDefaultAsync(existingProposal => existingProposal.ProposalsId == proposalId, cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        ProposalModerationGuard.EnsureActive(proposal);

        if (proposal.FreelancerProfilesId != freelancerProfile.FreelancerProfilesId)
        {
            throw new ForbiddenAccessException("You do not have permission to manage interview review for this proposal.");
        }

        if (proposal.Status != 0)
        {
            throw new BadRequestException("Interview review can only be used while the proposal is draft.");
        }

        return proposal;
    }

    private async Task EnsureInterviewCompletedAsync(
        Proposal proposal,
        Guid freelancerUserId,
        CancellationToken cancellationToken)
    {
        var requiredQuestionIds = await _context.Set<JobPostQuestion>()
            .AsNoTracking()
            .Where(question => question.JobPostsId == proposal.JobPostsId && question.IsRequired)
            .Select(question => question.JobPostQuestionsId)
            .ToListAsync(cancellationToken);

        if (requiredQuestionIds.Count == 0)
        {
            return;
        }

        var timers = await _context.Set<ProposalQuestionTimer>()
            .Where(timer => timer.ProposalsId == proposal.ProposalsId &&
                            timer.FreelancerUserId == freelancerUserId &&
                            requiredQuestionIds.Contains(timer.JobPostQuestionsId))
            .ToListAsync(cancellationToken);

        var now = _dateTimeService.UtcNow;
        foreach (var timer in timers.Where(timer => !timer.IsLocked && timer.ExpiresAt <= now))
        {
            timer.IsLocked = true;
            timer.LockedReason = (int)QuestionTimerLockedReason.Timeout;
            timer.CompletedAt = timer.CompletedAt ?? timer.ExpiresAt;
            timer.UpdatedAt = now;
        }

        if (timers.Count != requiredQuestionIds.Count || timers.Any(timer => !timer.IsLocked))
        {
            throw new BadRequestException("All required questions must be completed or timed out before review.");
        }
    }

    private async Task<List<Guid>> GetReviewableQuestionIdsAsync(
        Proposal proposal,
        CancellationToken cancellationToken)
    {
        return await _context.Set<ProposalAnswer>()
            .AsNoTracking()
            .Where(answer => answer.ProposalsId == proposal.ProposalsId &&
                             answer.AnswerText != string.Empty)
            .Join(
                _context.Set<JobPostQuestion>().AsNoTracking(),
                answer => answer.JobPostQuestionsId,
                question => question.JobPostQuestionsId,
                (answer, question) => new { answer.JobPostQuestionsId, question.JobPostsId, question.OrderIndex })
            .Where(item => item.JobPostsId == proposal.JobPostsId)
            .OrderBy(item => item.OrderIndex)
            .Select(item => item.JobPostQuestionsId)
            .ToListAsync(cancellationToken);
    }

    private async Task<ProposalInterviewReviewSession> GetSessionAsync(
        Guid proposalId,
        Guid freelancerUserId,
        CancellationToken cancellationToken)
    {
        var session = await _context.Set<ProposalInterviewReviewSession>()
            .FirstOrDefaultAsync(
                existingSession => existingSession.ProposalsId == proposalId,
                cancellationToken);

        if (session is null)
        {
            throw new BadRequestException("Interview review session has not been started.");
        }

        EnsureSessionOwner(session, freelancerUserId);
        return session;
    }

    private async Task LockExpiredSessionAsync(
        ProposalInterviewReviewSession session,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (session.IsLocked || session.ExpiresAt > now)
        {
            return;
        }

        session.IsLocked = true;
        session.CompletedAt = session.CompletedAt ?? session.ExpiresAt;
        session.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureSessionOwner(
        ProposalInterviewReviewSession session,
        Guid freelancerUserId)
    {
        if (session.FreelancerUserId != freelancerUserId)
        {
            throw new ForbiddenAccessException("You do not have permission to manage this interview review session.");
        }
    }

    private static InterviewReviewSessionDto ToDto(
        ProposalInterviewReviewSession session,
        IReadOnlyCollection<Guid> reviewableQuestionIds,
        DateTime now)
    {
        var remainingSeconds = session.IsLocked
            ? 0
            : Math.Max(0, (int)Math.Ceiling((session.ExpiresAt - now).TotalSeconds));

        return new InterviewReviewSessionDto(
            session.ProposalsId,
            session.StartedAt,
            session.ExpiresAt,
            remainingSeconds,
            session.IsLocked || session.ExpiresAt <= now,
            session.ReviewableQuestionCount,
            reviewableQuestionIds);
    }
}
