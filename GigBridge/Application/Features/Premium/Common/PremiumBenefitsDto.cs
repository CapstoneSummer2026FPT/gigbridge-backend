namespace Application.Features.Premium.Common;

public sealed record PremiumBenefitsDto(
    bool IsPremium,
    bool IsIdentityVerified,
    bool ShowProVerifiedBadge,
    DateTime? PremiumUntil,
    string? PlanName);
