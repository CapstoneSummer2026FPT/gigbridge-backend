using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Elo.Queries.GetEloTransactionDetail;

public sealed class GetEloTransactionDetailQueryHandler :
    IRequestHandler<GetEloTransactionDetailQuery, EloTransactionDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetEloTransactionDetailQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<EloTransactionDetailDto> Handle(
        GetEloTransactionDetailQuery query,
        CancellationToken cancellationToken)
    {
        var transaction = await _context.Set<UserEloPointTransaction>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.UserEloPointTransactionsId == query.TransactionId && x.UserId == query.UserId,
                cancellationToken)
            ?? throw new NotFoundException("Elo transaction does not exist.");

        var activeAppeal = await _context.Set<EloPointAppeal>()
            .AsNoTracking()
            .Where(x => x.EloPointTransactionId == query.TransactionId &&
                        (x.Status == (int)Domain.Enums.Elo.EloPointAppealStatus.Pending ||
                         x.Status == (int)Domain.Enums.Elo.EloPointAppealStatus.UnderReview))
            .Select(x => new EloAppealDto(
                x.EloPointAppealId, x.UserId, x.EloPointTransactionId, x.Status, x.Resolution,
                x.Reason, x.ResolutionNote, x.CorrectedDelta, x.AppliedTransactionId,
                x.ReviewedByAdminId, x.ReviewedAt, x.CancelledById, x.CancelledAt,
                x.CreatedAt, x.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return new EloTransactionDetailDto(ToDto(transaction), activeAppeal);
    }

    internal static EloTransactionDto ToDto(UserEloPointTransaction x) => new(
        x.UserEloPointTransactionsId, x.UserId, x.PointsDelta, x.PointsBefore, x.PointsAfter,
        x.Reason, x.SourceType, x.Mode, x.SourceEntityType, x.SourceEntityId,
        x.ContractId, x.ReviewId, x.Rating, x.EloAppealId, x.AppliedByAdminId, x.CreatedAt);
}
