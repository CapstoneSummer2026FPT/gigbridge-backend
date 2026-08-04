namespace Application.Features.Proposals.Freelancer.QuestionTimers.DTOs;

public record QuestionTimerStateDto(
    Guid ProposalId,
    Guid JobPostQuestionId,
    DateTime StartedAt,
    DateTime ExpiresAt,
    int RemainingSeconds,
    bool IsLocked,
    int? LockedReason);
