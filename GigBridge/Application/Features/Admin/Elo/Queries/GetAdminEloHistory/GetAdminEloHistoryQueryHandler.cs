using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Admin.Elo.DTOs;
using Application.Features.Elo.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Elo.Queries.GetAdminEloHistory;

public sealed class GetAdminEloHistoryQueryHandler :
    IRequestHandler<GetAdminEloHistoryQuery, PaginatedList<AdminEloTransactionRowDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminEloHistoryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedList<AdminEloTransactionRowDto>> Handle(
        GetAdminEloHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, PaginatedQuery.MaxPageSize);
        var filter = EloHistoryFilters.ParseOrDefault(query.Filter);

        var rows = _context.Set<UserEloPointTransaction>()
            .AsNoTracking()
            .Include(x => x.User);

        var filtered = EloHistoryFilters.Apply(rows, filter);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            filtered = filtered.Where(x =>
                x.User.FullName.ToLower().Contains(term) ||
                x.User.Email.ToLower().Contains(term));
        }

        var count = await filtered.CountAsync(cancellationToken);
        var items = await filtered
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new AdminEloTransactionRowDto(
                x.UserEloPointTransactionsId,
                new AdminEloUserInfoDto(x.User.UserId, x.User.FullName, x.User.Avatar, x.User.Email, x.User.Role),
                x.PointsDelta, x.PointsBefore, x.PointsAfter, x.Reason, x.SourceType, x.Mode,
                x.SourceEntityType, x.SourceEntityId, x.ContractId, x.ReviewId, x.Rating,
                x.EloAppealId, x.AppliedByAdminId, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PaginatedList<AdminEloTransactionRowDto>(items, count, page, size);
    }
}
