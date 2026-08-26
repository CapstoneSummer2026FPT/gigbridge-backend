using Application.Common.Constants;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Elo.Common;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Elo.Queries.GetEloHistory;

public sealed class GetEloHistoryQueryHandler : IRequestHandler<GetEloHistoryQuery, PaginatedList<EloTransactionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEloHistoryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedList<EloTransactionDto>> Handle(
        GetEloHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, PaginationDefaults.MaxPageSize);
        var filter = EloHistoryFilters.ParseOrDefault(query.Filter);

        var rows = _context.Set<UserEloPointTransaction>()
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId);
        rows = EloHistoryFilters.Apply(rows, filter);

        var count = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new EloTransactionDto(
                x.UserEloPointTransactionsId, x.UserId, x.PointsDelta, x.PointsBefore, x.PointsAfter,
                x.Reason, x.SourceType, x.Mode, x.SourceEntityType, x.SourceEntityId,
                x.ContractId, x.ReviewId, x.Rating, x.EloAppealId, x.AppliedByAdminId, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PaginatedList<EloTransactionDto>(items, count, page, size);
    }
}
