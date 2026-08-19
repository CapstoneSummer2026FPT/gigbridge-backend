namespace Application.Common.InternalServices.Proposals.Models;
public record QuestionTimerStateDto(
    Guid ProposalId,
    Guid JobPostQuestionId,
    DateTime StartedAt,
    DateTime ExpiresAt,
    int RemainingSeconds,
    bool IsLocked,
    int? LockedReason);
