using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Elo.Queries.GetMyEloAppeals;

public sealed class GetMyEloAppealsQueryHandler :
    IRequestHandler<GetMyEloAppealsQuery, PaginatedList<EloAppealDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyEloAppealsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedList<EloAppealDto>> Handle(
        GetMyEloAppealsQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);

        var rows = _context.Set<EloPointAppeal>()
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId);
        if (query.Status.HasValue)
            rows = rows.Where(x => x.Status == query.Status.Value);

        var count = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new EloAppealDto(
                x.EloPointAppealId, x.UserId, x.EloPointTransactionId, x.Status, x.Resolution,
                x.Reason, x.ResolutionNote, x.CorrectedDelta, x.AppliedTransactionId,
                x.ReviewedByAdminId, x.ReviewedAt, x.CancelledById, x.CancelledAt,
                x.CreatedAt, x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PaginatedList<EloAppealDto>(items, count, page, size);
    }
}
