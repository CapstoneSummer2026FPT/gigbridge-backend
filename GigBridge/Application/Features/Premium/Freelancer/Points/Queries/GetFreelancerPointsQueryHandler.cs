using Application.Common.InternalServices.Premium.Services;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Premium.Interfaces;
using Application.Features.Premium.Common;
using Application.Features.Premium.Freelancer.Points.DTOs;
using Domain.Entities;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.Points.Queries;

public sealed class GetFreelancerPointsQueryHandler : IRequestHandler<GetFreelancerPointsQuery, FreelancerPointsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPremiumAccessService _premiumAccess;
    public GetFreelancerPointsQueryHandler(IApplicationDbContext context, IPremiumAccessService premiumAccess) { _context = context; _premiumAccess = premiumAccess; }

    public async Task<FreelancerPointsDto> Handle(GetFreelancerPointsQuery query, CancellationToken ct)
    {
        var points = await _context.Set<UserEloScore>().AsNoTracking().Where(x => x.UserId == query.UserId).Select(x => (int?)x.CurrentPoints).FirstOrDefaultAsync(ct) ?? UserEloCalculator.DefaultPoints;
        var transactions = await _context.Set<UserEloPointTransaction>().AsNoTracking().Where(x => x.UserId == query.UserId).OrderByDescending(x => x.CreatedAt).Take(20).Select(x => new EloPointTransactionDto(x.UserEloPointTransactionsId, x.PointsDelta, x.PointsBefore, x.PointsAfter, x.Reason, x.SourceEntityType, x.SourceEntityId, x.CreatedAt)).ToListAsync(ct);
        var premium = await _premiumAccess.GetPremiumBenefitsAsync(query.UserId, ct);
        if (!premium.IsPremium) return new FreelancerPointsDto(points, false, null, null, null, null, null, transactions);
        var setting = await _context.Set<PlatformSetting>().AsNoTracking().Where(x => x.Key == PremiumTierCalculator.SettingKey).Select(x => x.Value).FirstOrDefaultAsync(ct);
        var tier = PremiumTierCalculator.Calculate(points, setting);
        return new FreelancerPointsDto(points, true, tier.Name, tier.Threshold, tier.NextName, tier.NextThreshold, tier.Progress, transactions);
    }
}
