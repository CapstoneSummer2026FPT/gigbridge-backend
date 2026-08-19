namespace Application.Common.InternalServices.Proposals.Models;
public record InterviewReviewSessionDto(
    Guid ProposalId,
    DateTime StartedAt,
    DateTime ExpiresAt,
    int RemainingSeconds,
    bool IsLocked,
    int ReviewableQuestionCount,
    IReadOnlyCollection<Guid> ReviewableQuestionIds);
