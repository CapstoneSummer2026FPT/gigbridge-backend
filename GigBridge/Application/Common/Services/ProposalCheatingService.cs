using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;
using Application.Features.Proposals.Freelancer.Cheating.DTOs;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Services;

public class ProposalCheatingService : IProposalCheatingService
{
    private const int CheatingPenaltyPoints = -50;
    private const int SuspensionThreshold = 3;
    private const int SuspensionDays = 7;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IUserEloService _userEloService;

    public ProposalCheatingService(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IUserEloService userEloService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _userEloService = userEloService;
    }

    public async Task<CheatingEventLogResponse> LogEventAsync(
        Guid proposalId,
        Guid freelancerUserId,
        LogProposalCheatingEventRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(CheatingEventType), request.EventType))
        {
            throw new BadRequestException("Invalid cheating event type.");
        }

        var clientEventId = request.ClientEventId?.Trim();
        if (string.IsNullOrWhiteSpace(clientEventId))
        {
            throw new BadRequestException("ClientEventId is required.");
        }

        var proposal = await GetOwnedDraftProposalAsync(proposalId, freelancerUserId, cancellationToken);
        await EnsureQuestionBelongsToProposalAsync(proposal, request.JobPostQuestionId, cancellationToken);

        var existingEvent = await _context.Set<ProposalCheatingEvent>()
            .AsNoTracking()
            .AnyAsync(
                cheatingEvent => cheatingEvent.ProposalsId == proposalId &&
                                 cheatingEvent.ClientEventId == clientEventId,
                cancellationToken);

        if (!existingEvent)
        {
            var now = _dateTimeService.UtcNow;
            _context.Set<ProposalCheatingEvent>().Add(new ProposalCheatingEvent
            {
                ProposalCheatingEventsId = Guid.NewGuid(),
                ProposalsId = proposalId,
                FreelancerUserId = freelancerUserId,
                JobPostQuestionsId = request.JobPostQuestionId,
                EventType = request.EventType,
                ClientEventId = clientEventId,
                IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress,
                UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
                Metadata = request.Metadata is null ? null : JsonSerializer.Serialize(request.Metadata),
                OccurredAt = request.OccurredAt ?? now,
                CreatedAt = now
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        return await BuildSessionSummaryAsync(proposalId, request.EventType, cancellationToken);
    }

    public async Task<CheatingPenaltyResultDto?> ApplySubmissionPenaltyIfNeededAsync(
        Proposal proposal,
        Guid freelancerUserId,
        CancellationToken cancellationToken)
    {
        var existingViolation = await _context.Set<FreelancerCheatingViolation>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                violation => violation.ProposalsId == proposal.ProposalsId,
                cancellationToken);

        if (existingViolation is not null)
        {
            return ToPenaltyResult(existingViolation);
        }

        var events = await _context.Set<ProposalCheatingEvent>()
            .Where(cheatingEvent => cheatingEvent.ProposalsId == proposal.ProposalsId &&
                                    cheatingEvent.FreelancerUserId == freelancerUserId)
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return null;
        }

        var previousViolationCount = await _context.Set<FreelancerCheatingViolation>()
            .CountAsync(
                violation => violation.FreelancerUserId == freelancerUserId &&
                             violation.ProposalsId != proposal.ProposalsId,
                cancellationToken);

        var violationNumber = previousViolationCount + 1;
        var now = _dateTimeService.UtcNow;
        var action = violationNumber >= SuspensionThreshold
            ? CheatingViolationAction.TemporarySuspension
            : CheatingViolationAction.EloPenalty;

        DateTime? suspendedUntil = null;
        if (action == CheatingViolationAction.TemporarySuspension)
        {
            suspendedUntil = now.AddDays(SuspensionDays);
            var user = await _context.Set<User>()
                .FirstOrDefaultAsync(existingUser => existingUser.UserId == freelancerUserId, cancellationToken);

            if (user is null)
            {
                throw new NotFoundException("Freelancer does not exist.");
            }

            user.SuspendedAt = now;
            user.SuspendedUntil = suspendedUntil;
            user.SuspensionReason = "Suspended for repeated cheating during interview questions.";
            user.UpdatedAt = now;
        }

        var violation = new FreelancerCheatingViolation
        {
            FreelancerCheatingViolationsId = Guid.NewGuid(),
            ProposalsId = proposal.ProposalsId,
            FreelancerUserId = freelancerUserId,
            ViolationNumber = violationNumber,
            TotalEventCount = events.Count,
            CopyCount = events.Count(cheatingEvent => cheatingEvent.EventType == (int)CheatingEventType.Copy),
            PasteCount = events.Count(cheatingEvent => cheatingEvent.EventType == (int)CheatingEventType.Paste),
            TabSwitchCount = events.Count(cheatingEvent => cheatingEvent.EventType == (int)CheatingEventType.TabSwitch),
            ScreenshotAttemptCount = events.Count(cheatingEvent => cheatingEvent.EventType == (int)CheatingEventType.ScreenshotAttempt),
            FocusLossCount = events.Count(cheatingEvent => cheatingEvent.EventType == (int)CheatingEventType.FocusLoss),
            FullscreenExitCount = events.Count(cheatingEvent => cheatingEvent.EventType == (int)CheatingEventType.FullscreenExit),
            Action = (int)action,
            EloDelta = CheatingPenaltyPoints,
            SuspendedUntil = suspendedUntil,
            IsReviewed = false,
            CreatedAt = now
        };

        _context.Set<FreelancerCheatingViolation>().Add(violation);
        await _userEloService.ApplyCheatingPenaltyAsync(
            violation.FreelancerCheatingViolationsId,
            freelancerUserId,
            CheatingPenaltyPoints,
            cancellationToken);

        return ToPenaltyResult(violation);
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

        if (proposal.FreelancerProfilesId != freelancerProfile.FreelancerProfilesId)
        {
            throw new ForbiddenAccessException("You do not have permission to log cheating events for this proposal.");
        }

        if (proposal.Status != 0)
        {
            throw new BadRequestException("Cheating events can only be logged while the proposal is draft.");
        }

        return proposal;
    }

    private async Task EnsureQuestionBelongsToProposalAsync(
        Proposal proposal,
        Guid? jobPostQuestionId,
        CancellationToken cancellationToken)
    {
        if (!jobPostQuestionId.HasValue)
        {
            return;
        }

        var question = await _context.Set<JobPostQuestion>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                existingQuestion => existingQuestion.JobPostQuestionsId == jobPostQuestionId.Value,
                cancellationToken);

        if (question is null)
        {
            throw new NotFoundException("Question does not exist.");
        }

        if (question.JobPostsId != proposal.JobPostsId)
        {
            throw new BadRequestException("Question does not belong to this proposal's job post.");
        }
    }

    private async Task<CheatingEventLogResponse> BuildSessionSummaryAsync(
        Guid proposalId,
        int eventType,
        CancellationToken cancellationToken)
    {
        var counts = await _context.Set<ProposalCheatingEvent>()
            .AsNoTracking()
            .Where(cheatingEvent => cheatingEvent.ProposalsId == proposalId)
            .GroupBy(cheatingEvent => cheatingEvent.EventType)
            .Select(group => new { EventType = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var copyCount = counts.FirstOrDefault(item => item.EventType == (int)CheatingEventType.Copy)?.Count ?? 0;
        var pasteCount = counts.FirstOrDefault(item => item.EventType == (int)CheatingEventType.Paste)?.Count ?? 0;
        var tabSwitchCount = counts.FirstOrDefault(item => item.EventType == (int)CheatingEventType.TabSwitch)?.Count ?? 0;
        var screenshotAttemptCount = counts.FirstOrDefault(item => item.EventType == (int)CheatingEventType.ScreenshotAttempt)?.Count ?? 0;
        var focusLossCount = counts.FirstOrDefault(item => item.EventType == (int)CheatingEventType.FocusLoss)?.Count ?? 0;
        var fullscreenExitCount = counts.FirstOrDefault(item => item.EventType == (int)CheatingEventType.FullscreenExit)?.Count ?? 0;
        var totalCount = counts.Sum(item => item.Count);

        return new CheatingEventLogResponse(
            proposalId,
            eventType,
            totalCount,
            copyCount,
            pasteCount,
            tabSwitchCount,
            screenshotAttemptCount,
            focusLossCount,
            fullscreenExitCount,
            "Cheating behavior detected. Continued violations may reduce Elo points or suspend your account.");
    }

    private static CheatingPenaltyResultDto ToPenaltyResult(FreelancerCheatingViolation violation)
    {
        var message = violation.SuspendedUntil.HasValue
            ? "Cheating penalty applied: 50 Elo points deducted and account suspended for 7 days."
            : "Cheating penalty applied: 50 Elo points deducted.";

        return new CheatingPenaltyResultDto(
            true,
            violation.FreelancerCheatingViolationsId,
            violation.ViolationNumber,
            violation.EloDelta,
            violation.Action,
            violation.SuspendedUntil,
            message);
    }
}
