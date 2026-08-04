using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Premium;

public sealed record AdminPremiumSubscriptionDto(
    Guid Id, string Plan, int Status, DateTime StartDate, DateTime EndDate,
    bool AutoRenew, DateTime? CancelledAt, string? PaymentReference);
public sealed record AdminRankProtectionDto(
    Guid Id, bool IsEnabled, DateTime StartsAt, DateTime EndsAt, DateTime? CancelledAt);
public sealed record AdminPromotionDto(
    Guid Id, string Package, int Status, decimal BoostWeight, decimal TokenCost,
    DateTime StartsAt, DateTime EndsAt, Guid? WalletTransactionId);
public sealed record PremiumUserDiagnosticsDto(
    Guid UserId, string Email,
    IReadOnlyList<AdminPremiumSubscriptionDto> Subscriptions,
    IReadOnlyList<AdminRankProtectionDto> RankProtections,
    IReadOnlyList<AdminPromotionDto> Promotions);

public sealed record GetPremiumUserDiagnosticsQuery(Guid UserId)
    : IRequest<PremiumUserDiagnosticsDto>;

public sealed class GetPremiumUserDiagnosticsQueryHandler :
    IRequestHandler<GetPremiumUserDiagnosticsQuery, PremiumUserDiagnosticsDto>
{
    private readonly IApplicationDbContext _context;
    public GetPremiumUserDiagnosticsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PremiumUserDiagnosticsDto> Handle(
        GetPremiumUserDiagnosticsQuery request, CancellationToken ct)
    {
        var email = await _context.Set<User>().AsNoTracking()
            .Where(x => x.UserId == request.UserId)
            .Select(x => x.Email).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("User", request.UserId);
        var subscriptions = await _context.Set<Subscription>().AsNoTracking()
            .Where(x => x.UserId == request.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminPremiumSubscriptionDto(
                x.SubscriptionsId, x.SubscriptionPlans.Name, (int)x.Status,
                x.StartDate, x.EndDate, x.AutoRenew ?? false,
                x.CancelledAt, x.PaymentReference))
            .ToListAsync(ct);
        var protections = await _context.Set<FreelancerRankProtection>().AsNoTracking()
            .Where(x => x.FreelancerProfile.UserId == request.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminRankProtectionDto(
                x.FreelancerRankProtectionsId, x.IsVacationModeEnabled,
                x.RankProtectionStartedAt, x.RankProtectionEndsAt, x.CancelledAt))
            .ToListAsync(ct);
        var promotions = await _context.Set<FreelancerProfilePromotion>().AsNoTracking()
            .Where(x => x.FreelancerProfile.UserId == request.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminPromotionDto(
                x.FreelancerProfilePromotionsId, x.PackageName, (int)x.Status,
                x.BoostWeight, x.TokenCost, x.StartTime, x.EndTime, x.WalletTransactionId))
            .ToListAsync(ct);
        return new PremiumUserDiagnosticsDto(
            request.UserId, email, subscriptions, protections, promotions);
    }
}
