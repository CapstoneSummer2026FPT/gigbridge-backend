using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Elo.Common;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Elo.Queries.GetEloAppealDetail;

public sealed class GetEloAppealDetailQueryHandler : IRequestHandler<GetEloAppealDetailQuery, EloAppealDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetEloAppealDetailQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<EloAppealDetailDto> Handle(
        GetEloAppealDetailQuery query,
        CancellationToken cancellationToken)
    {
        var appeal = await _context.Set<EloPointAppeal>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EloPointAppealId == query.AppealId && x.UserId == query.UserId,
                cancellationToken)
            ?? throw new NotFoundException("Elo appeal does not exist.");

        var appealDto = EloAppealMappings.ToDto(appeal);

        var transaction = await _context.Set<UserEloPointTransaction>()
            .AsNoTracking()
            .Where(x => x.UserEloPointTransactionsId == appeal.EloPointTransactionId)
            .Select(x => new EloTransactionDto(
                x.UserEloPointTransactionsId, x.UserId, x.PointsDelta, x.PointsBefore, x.PointsAfter,
                x.Reason, x.SourceType, x.Mode, x.SourceEntityType, x.SourceEntityId,
                x.ContractId, x.ReviewId, x.Rating, x.EloAppealId, x.AppliedByAdminId, x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        var evidence = await _context.Set<EloPointAppealEvidence>()
            .AsNoTracking()
            .Where(x => x.EloPointAppealId == appeal.EloPointAppealId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new EloAppealEvidenceDto(
                x.EloPointAppealEvidenceId, x.EloPointAppealId, x.UploadedById, x.FileName,
                x.FileUrl, x.FileSize, x.Description, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new EloAppealDetailDto(appealDto, transaction, evidence);
    }
}
