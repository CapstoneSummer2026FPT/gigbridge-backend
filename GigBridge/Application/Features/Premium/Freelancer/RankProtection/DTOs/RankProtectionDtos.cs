namespace Application.Features.Premium.Freelancer.RankProtection.DTOs;
public sealed record RankProtectionDto(Guid Id, bool IsEnabled, DateTime StartsAt, DateTime EndsAt, string? Reason, DateTime? CancelledAt);
public sealed record ActivateRankProtectionRequest(DateTime? StartsAt, DateTime EndsAt, string? Reason);
