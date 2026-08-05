using Application.Common.Interfaces;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Elo.Queries.GetEloSummary;

public sealed class GetEloSummaryQueryHandler : IRequestHandler<GetEloSummaryQuery, EloSummaryDto>
{
    private const int RecentTransactionCount = 20;
    private readonly IApplicationDbContext _context;

    public GetEloSummaryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<EloSummaryDto> Handle(GetEloSummaryQuery query, CancellationToken cancellationToken)
    {
        var points = await _context.Set<UserEloScore>()
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId)
            .Select(x => (int?)x.CurrentPoints)
            .FirstOrDefaultAsync(cancellationToken)
            ?? UserEloCalculator.DefaultPoints;

        var transactions = _context.Set<UserEloPointTransaction>()
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId);

        var gained = await transactions.Where(x => x.PointsDelta > 0)
            .SumAsync(x => (int?)x.PointsDelta, cancellationToken) ?? 0;
        var lost = await transactions.Where(x => x.PointsDelta < 0)
            .SumAsync(x => x.PointsDelta, cancellationToken);
        var total = await transactions.CountAsync(cancellationToken);

        var recent = await transactions
            .OrderByDescending(x => x.CreatedAt)
            .Take(RecentTransactionCount)
            .Select(x => new EloTransactionDto(
                x.UserEloPointTransactionsId, x.UserId, x.PointsDelta, x.PointsBefore, x.PointsAfter,
                x.Reason, x.SourceType, x.Mode, x.SourceEntityType, x.SourceEntityId,
                x.ContractId, x.ReviewId, x.Rating, x.EloAppealId, x.AppliedByAdminId, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new EloSummaryDto(points, gained, lost, total, recent);
    }
}
