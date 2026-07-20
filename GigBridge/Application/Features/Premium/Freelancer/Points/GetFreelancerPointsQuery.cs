using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Premium.Common;
using Domain.Entities;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Points;

public sealed record EloPointTransactionDto(
    Guid Id,
    int PointsDelta,
    int PointsBefore,
    int PointsAfter,
    int Reason,
    string? SourceEntityType,
    Guid? SourceEntityId,
    DateTime CreatedAt);

public sealed record FreelancerPointsDto(
    int EloPoints,
    bool IsPremium,
    string? TierName,
    int? TierThreshold,
    string? NextTierName,
    int? NextTierThreshold,
    decimal? TierProgress,
    IReadOnlyList<EloPointTransactionDto> RecentTransactions);

public sealed record GetFreelancerPointsQuery(Guid UserId) : IRequest<FreelancerPointsDto>;

public sealed class GetFreelancerPointsQueryHandler :
    IRequestHandler<GetFreelancerPointsQuery, FreelancerPointsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPremiumAccessService _premiumAccess;

    public GetFreelancerPointsQueryHandler(
        IApplicationDbContext context,
        IPremiumAccessService premiumAccess)
    {
        _context = context;
        _premiumAccess = premiumAccess;
    }

    public async Task<FreelancerPointsDto> Handle(
        GetFreelancerPointsQuery request,
        CancellationToken cancellationToken)
    {
        var points = await _context.Set<UserEloScore>()
            .AsNoTracking()
            .Where(item => item.UserId == request.UserId)
            .Select(item => (int?)item.CurrentPoints)
            .FirstOrDefaultAsync(cancellationToken) ?? UserEloCalculator.DefaultPoints;

        var transactions = await _context.Set<UserEloPointTransaction>()
            .AsNoTracking()
            .Where(item => item.UserId == request.UserId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(20)
            .Select(item => new EloPointTransactionDto(
                item.UserEloPointTransactionsId,
                item.PointsDelta,
                item.PointsBefore,
                item.PointsAfter,
                item.Reason,
                item.SourceEntityType,
                item.SourceEntityId,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        var premium = await _premiumAccess.GetPremiumBenefitsAsync(request.UserId, cancellationToken);
        if (!premium.IsPremium)
            return new FreelancerPointsDto(points, false, null, null, null, null, null, transactions);

        var setting = await _context.Set<PlatformSetting>()
            .AsNoTracking()
            .Where(item => item.Key == PremiumTierCalculator.SettingKey)
            .Select(item => item.Value)
            .FirstOrDefaultAsync(cancellationToken);
        var tier = PremiumTierCalculator.Calculate(points, setting);

        return new FreelancerPointsDto(
            points,
            true,
            tier.Name,
            tier.Threshold,
            tier.NextName,
            tier.NextThreshold,
            tier.Progress,
            transactions);
    }
}
