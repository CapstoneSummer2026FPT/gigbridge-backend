using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Admin.Elo.DTOs;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Elo.Queries.GetAdminEloUserSummary;

public sealed class GetAdminEloUserSummaryQueryHandler :
    IRequestHandler<GetAdminEloUserSummaryQuery, AdminEloUserSummaryDto>
{
    private const int RecentTransactionCount = 20;
    private readonly IApplicationDbContext _context;

    public GetAdminEloUserSummaryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<AdminEloUserSummaryDto> Handle(
        GetAdminEloUserSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == query.UserId, cancellationToken)
            ?? throw new NotFoundException("User does not exist.");

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

        return new AdminEloUserSummaryDto(
            new AdminEloUserInfoDto(user.UserId, user.FullName, user.Avatar, user.Email, user.Role),
            points, gained, lost, total, recent);
    }
}
