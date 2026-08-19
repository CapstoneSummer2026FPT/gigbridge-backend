namespace Application.Common.InternalServices.Premium.Models;
public sealed record PremiumBenefitsDto(
    bool IsPremium,
    bool IsIdentityVerified,
    bool ShowProVerifiedBadge,
    DateTime? PremiumUntil,
    string? PlanName);
