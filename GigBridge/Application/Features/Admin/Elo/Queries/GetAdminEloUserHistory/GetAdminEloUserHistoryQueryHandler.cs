using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Admin.Elo.DTOs;
using Application.Features.Elo.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Elo.Queries.GetAdminEloUserHistory;

public sealed class GetAdminEloUserHistoryQueryHandler :
    IRequestHandler<GetAdminEloUserHistoryQuery, PaginatedList<AdminEloTransactionRowDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminEloUserHistoryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedList<AdminEloTransactionRowDto>> Handle(
        GetAdminEloUserHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, PaginatedQuery.MaxPageSize);
        var filter = EloHistoryFilters.ParseOrDefault(query.Filter);

        if (!await _context.Set<User>()
                .AsNoTracking()
                .AnyAsync(x => x.UserId == query.UserId, cancellationToken))
            throw new NotFoundException("User does not exist.");

        var rows = _context.Set<UserEloPointTransaction>()
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.UserId == query.UserId);
        rows = EloHistoryFilters.Apply(rows, filter);

        var count = await rows.CountAsync(cancellationToken);
        var items = await rows
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
