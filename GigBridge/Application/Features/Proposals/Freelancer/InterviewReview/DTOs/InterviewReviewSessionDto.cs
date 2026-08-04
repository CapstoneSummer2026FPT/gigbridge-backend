namespace Application.Features.Proposals.Freelancer.InterviewReview.DTOs;

public record InterviewReviewSessionDto(
    Guid ProposalId,
    DateTime StartedAt,
    DateTime ExpiresAt,
    int RemainingSeconds,
    bool IsLocked,
    int ReviewableQuestionCount,
    IReadOnlyCollection<Guid> ReviewableQuestionIds);
