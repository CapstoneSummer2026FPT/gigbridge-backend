using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Proposals.Interfaces;
using Application.Common.InternalServices.Proposals.Services;
using Application.Features.Proposals.Common;
using Application.Features.Proposals.Freelancer.Answers;
using Application.Common.InternalServices.Proposals.Models;
using Domain.Entities;
using Domain.Enums.Proposals;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.InternalServices.Proposals.Services;
public class ProposalQuestionTimerService : IProposalQuestionTimerService
{
    private const int QuestionDurationSeconds = 180;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public ProposalQuestionTimerService(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<QuestionTimerStateDto> StartTimerAsync(
        Guid proposalId,
        Guid jobPostQuestionId,
        Guid freelancerUserId,
        CancellationToken cancellationToken)
    {
        var proposal = await GetOwnedDraftProposalAsync(proposalId, freelancerUserId, cancellationToken);
        await GetQuestionForProposalAsync(proposal, jobPostQuestionId, cancellationToken);

        var timer = await _context.Set<ProposalQuestionTimer>()
            .FirstOrDefaultAsync(
                existingTimer => existingTimer.ProposalsId == proposalId &&
                                 existingTimer.JobPostQuestionsId == jobPostQuestionId,
                cancellationToken);

        var now = _dateTimeService.UtcNow;
        if (timer is null)
        {
            timer = new ProposalQuestionTimer
            {
                ProposalQuestionTimersId = Guid.NewGuid(),
                ProposalsId = proposalId,
                JobPostQuestionsId = jobPostQuestionId,
                FreelancerUserId = freelancerUserId,
                StartedAt = now,
                ExpiresAt = now.AddSeconds(QuestionDurationSeconds),
                IsLocked = false,
                CreatedAt = now
            };

            _context.Set<ProposalQuestionTimer>().Add(timer);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return ToDto(timer, now);
    }

    public async Task<QuestionTimerStateDto> CompleteTimerAsync(
        Guid proposalId,
        Guid jobPostQuestionId,
        Guid freelancerUserId,
        CompleteQuestionTimerRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(QuestionTimerLockedReason), request.LockedReason))
        {
            throw new BadRequestException("Invalid question timer locked reason.");
        }

        var proposal = await GetOwnedDraftProposalAsync(proposalId, freelancerUserId, cancellationToken);
        var question = await GetQuestionForProposalAsync(proposal, jobPostQuestionId, cancellationToken);
        var timer = await GetTimerAsync(proposalId, jobPostQuestionId, freelancerUserId, cancellationToken);

        var now = _dateTimeService.UtcNow;
        var lockedReason = (QuestionTimerLockedReason)request.LockedReason;
        if (!timer.IsLocked && timer.ExpiresAt <= now)
        {
            lockedReason = QuestionTimerLockedReason.Timeout;
        }

        var normalizedAnswerText = ProposalAnswerCommandHelper.NormalizeAnswerText(request.AnswerText);

        if (lockedReason == QuestionTimerLockedReason.Completed)
        {
            ProposalAnswerCommandHelper.EnsureRequiredQuestionHasAnswer(question, normalizedAnswerText);
        }

        if (!timer.IsLocked)
        {
            await UpsertAnswerAsync(proposalId, jobPostQuestionId, normalizedAnswerText, now, cancellationToken);

            timer.IsLocked = true;
            timer.LockedReason = (int)lockedReason;
            timer.CompletedAt = now;
            timer.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return ToDto(timer, now);
    }

    public async Task EnsureQuestionCanBeModifiedAsync(
        Proposal proposal,
        Guid jobPostQuestionId,
        Guid freelancerUserId,
        CancellationToken cancellationToken)
    {
        var timer = await GetTimerAsync(
            proposal.ProposalsId,
            jobPostQuestionId,
            freelancerUserId,
            cancellationToken);

        var now = _dateTimeService.UtcNow;
        if (timer.IsLocked)
        {
            if (await CanEditDuringActiveReviewAsync(proposal, jobPostQuestionId, freelancerUserId, now, cancellationToken))
            {
                return;
            }

            throw new BadRequestException("This question is locked and can no longer be edited.");
        }

        if (timer.ExpiresAt <= now)
        {
            timer.IsLocked = true;
            timer.LockedReason = (int)QuestionTimerLockedReason.Timeout;
            timer.CompletedAt = timer.CompletedAt ?? timer.ExpiresAt;
            timer.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
            throw new BadRequestException("This question timer has expired and can no longer be edited.");
        }
    }

    private async Task<bool> CanEditDuringActiveReviewAsync(
        Proposal proposal,
        Guid jobPostQuestionId,
        Guid freelancerUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var session = await _context.Set<ProposalInterviewReviewSession>()
            .FirstOrDefaultAsync(
                existingSession => existingSession.ProposalsId == proposal.ProposalsId &&
                                   existingSession.FreelancerUserId == freelancerUserId,
                cancellationToken);

        if (session is null)
        {
            return false;
        }

        if (session.IsLocked)
        {
            return false;
        }

        if (session.ExpiresAt <= now)
        {
            session.IsLocked = true;
            session.CompletedAt = session.CompletedAt ?? session.ExpiresAt;
            session.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
            return false;
        }

        return await _context.Set<ProposalAnswer>()
            .AsNoTracking()
            .AnyAsync(
                answer => answer.ProposalsId == proposal.ProposalsId &&
                          answer.JobPostQuestionsId == jobPostQuestionId &&
                          answer.AnswerText != string.Empty,
                cancellationToken);
    }

    public async Task EnsureProposalReadyForSubmissionAsync(
        Proposal proposal,
        Guid freelancerUserId,
        CancellationToken cancellationToken)
    {
        var requiredQuestions = await _context.Set<JobPostQuestion>()
            .AsNoTracking()
            .Where(question => question.JobPostsId == proposal.JobPostsId && question.IsRequired)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync(cancellationToken);

        if (requiredQuestions.Count == 0)
        {
            return;
        }

        var requiredQuestionIds = requiredQuestions
            .Select(question => question.JobPostQuestionsId)
            .ToList();

        var timers = await _context.Set<ProposalQuestionTimer>()
            .Where(timer => timer.ProposalsId == proposal.ProposalsId &&
                            timer.FreelancerUserId == freelancerUserId &&
                            requiredQuestionIds.Contains(timer.JobPostQuestionsId))
            .ToListAsync(cancellationToken);

        var timersByQuestionId = timers.ToDictionary(timer => timer.JobPostQuestionsId);
        var now = _dateTimeService.UtcNow;

        foreach (var question in requiredQuestions)
        {
            if (!timersByQuestionId.TryGetValue(question.JobPostQuestionsId, out var timer))
            {
                throw new BadRequestException("All required questions must be completed or timed out before submitting.");
            }

            if (!timer.IsLocked && timer.ExpiresAt <= now)
            {
                timer.IsLocked = true;
                timer.LockedReason = (int)QuestionTimerLockedReason.Timeout;
                timer.CompletedAt = timer.CompletedAt ?? timer.ExpiresAt;
                timer.UpdatedAt = now;
            }

            if (!timer.IsLocked)
            {
                throw new BadRequestException("All required questions must be completed or timed out before submitting.");
            }

            if (timer.LockedReason == (int)QuestionTimerLockedReason.Timeout)
            {
                continue;
            }

            var hasAnswer = await _context.Set<ProposalAnswer>()
                .AsNoTracking()
                .AnyAsync(
                    answer => answer.ProposalsId == proposal.ProposalsId &&
                              answer.JobPostQuestionsId == question.JobPostQuestionsId &&
                              answer.AnswerText != string.Empty,
                    cancellationToken);

            if (!hasAnswer)
            {
                throw new BadRequestException("All required questions must be answered before submitting.");
            }
        }
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
            throw new ForbiddenAccessException("You do not have permission to manage timers for this proposal.");
        }

        if (proposal.Status != 0)
        {
            throw new BadRequestException("Question timers can only be used while the proposal is draft.");
        }

        return proposal;
    }

    private async Task<JobPostQuestion> GetQuestionForProposalAsync(
        Proposal proposal,
        Guid jobPostQuestionId,
        CancellationToken cancellationToken)
    {
        var question = await _context.Set<JobPostQuestion>()
            .FirstOrDefaultAsync(
                existingQuestion => existingQuestion.JobPostQuestionsId == jobPostQuestionId,
                cancellationToken);

        if (question is null)
        {
            throw new NotFoundException("Question does not exist.");
        }

        if (question.JobPostsId != proposal.JobPostsId)
        {
            throw new BadRequestException("Question does not belong to this proposal's job post.");
        }

        return question;
    }

    private async Task<ProposalQuestionTimer> GetTimerAsync(
        Guid proposalId,
        Guid jobPostQuestionId,
        Guid freelancerUserId,
        CancellationToken cancellationToken)
    {
        var timer = await _context.Set<ProposalQuestionTimer>()
            .FirstOrDefaultAsync(
                existingTimer => existingTimer.ProposalsId == proposalId &&
                                 existingTimer.JobPostQuestionsId == jobPostQuestionId,
                cancellationToken);

        if (timer is null)
        {
            throw new BadRequestException("Question timer has not been started.");
        }

        if (timer.FreelancerUserId != freelancerUserId)
        {
            throw new ForbiddenAccessException("You do not have permission to manage this question timer.");
        }

        return timer;
    }

    private async Task UpsertAnswerAsync(
        Guid proposalId,
        Guid jobPostQuestionId,
        string answerText,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var answer = await _context.Set<ProposalAnswer>()
            .FirstOrDefaultAsync(
                existingAnswer => existingAnswer.ProposalsId == proposalId &&
                                  existingAnswer.JobPostQuestionsId == jobPostQuestionId,
                cancellationToken);

        if (answer is null)
        {
            _context.Set<ProposalAnswer>().Add(new ProposalAnswer
            {
                ProposalAnswersId = Guid.NewGuid(),
                ProposalsId = proposalId,
                JobPostQuestionsId = jobPostQuestionId,
                AnswerText = answerText,
                CreatedAt = now
            });
            return;
        }

        answer.AnswerText = answerText;
        answer.UpdatedAt = now;
    }

    private static QuestionTimerStateDto ToDto(ProposalQuestionTimer timer, DateTime now)
    {
        var remainingSeconds = timer.IsLocked
            ? 0
            : Math.Max(0, (int)Math.Ceiling((timer.ExpiresAt - now).TotalSeconds));

        return new QuestionTimerStateDto(
            timer.ProposalsId,
            timer.JobPostQuestionsId,
            timer.StartedAt,
            timer.ExpiresAt,
            remainingSeconds,
            timer.IsLocked || timer.ExpiresAt <= now,
            timer.LockedReason);
    }
}
