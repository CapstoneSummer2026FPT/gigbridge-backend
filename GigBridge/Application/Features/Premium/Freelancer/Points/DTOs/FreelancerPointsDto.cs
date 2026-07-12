namespace Application.Features.Premium.Freelancer.Points.DTOs;

public sealed record EloPointTransactionDto(Guid Id, int PointsDelta, int PointsBefore, int PointsAfter, int Reason, string? SourceEntityType, Guid? SourceEntityId, DateTime CreatedAt);
public sealed record FreelancerPointsDto(int EloPoints, bool IsPremium, string? TierName, int? TierThreshold, string? NextTierName, int? NextTierThreshold, decimal? TierProgress, IReadOnlyList<EloPointTransactionDto> RecentTransactions);
