using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Admin.Elo.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Elo.Queries.GetEloAppeals;

public sealed class GetEloAppealsQueryHandler :
    IRequestHandler<GetEloAppealsQuery, PaginatedList<AdminEloAppealRowDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEloAppealsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedList<AdminEloAppealRowDto>> Handle(
        GetEloAppealsQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);

        var rows = _context.Set<EloPointAppeal>()
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.ReviewedByAdmin)
            .AsQueryable();

        if (query.Status.HasValue)
            rows = rows.Where(x => x.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            rows = rows.Where(x =>
                x.User.FullName.ToLower().Contains(term) ||
                x.User.Email.ToLower().Contains(term) ||
                x.Reason.ToLower().Contains(term));
        }

        var count = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new AdminEloAppealRowDto(
                x.EloPointAppealId,
                new AdminEloUserInfoDto(x.User.UserId, x.User.FullName, x.User.Avatar, x.User.Email, x.User.Role),
                x.EloPointTransactionId, x.Status, x.Resolution, x.Reason, x.ResolutionNote,
                x.CorrectedDelta, x.AppliedTransactionId, x.ReviewedByAdminId,
                x.ReviewedByAdmin != null ? x.ReviewedByAdmin.FullName : null,
                x.ReviewedAt, x.CancelledById, x.CancelledAt, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PaginatedList<AdminEloAppealRowDto>(items, count, page, size);
    }
}
