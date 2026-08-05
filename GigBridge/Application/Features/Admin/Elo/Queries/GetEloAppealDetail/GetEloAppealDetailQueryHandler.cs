using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Admin.Elo.DTOs;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Elo.Queries.GetEloAppealDetail;

public sealed class GetEloAppealDetailQueryHandler : IRequestHandler<GetEloAppealDetailQuery, AdminEloAppealDetailDto>
{
    private const int RecentTransactionCount = 20;
    private readonly IApplicationDbContext _context;

    public GetEloAppealDetailQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<AdminEloAppealDetailDto> Handle(
        GetEloAppealDetailQuery query,
        CancellationToken cancellationToken)
    {
        var appeal = await _context.Set<EloPointAppeal>()
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.ReviewedByAdmin)
            .FirstOrDefaultAsync(x => x.EloPointAppealId == query.AppealId, cancellationToken)
            ?? throw new NotFoundException("Elo appeal does not exist.");

        var userInfo = new AdminEloUserInfoDto(
            appeal.User.UserId, appeal.User.FullName, appeal.User.Avatar, appeal.User.Email, appeal.User.Role);

        var appealRow = new AdminEloAppealRowDto(
            appeal.EloPointAppealId, userInfo, appeal.EloPointTransactionId, appeal.Status,
            appeal.Resolution, appeal.Reason, appeal.ResolutionNote, appeal.CorrectedDelta,
            appeal.AppliedTransactionId, appeal.ReviewedByAdminId,
            appeal.ReviewedByAdmin != null ? appeal.ReviewedByAdmin.FullName : null,
            appeal.ReviewedAt, appeal.CancelledById, appeal.CancelledAt, appeal.CreatedAt, appeal.UpdatedAt);

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

        var summary = await LoadUserSummaryAsync(appeal.UserId, cancellationToken);

        return new AdminEloAppealDetailDto(appealRow, transaction, evidence, summary);
    }

    private async Task<AdminEloUserSummaryDto> LoadUserSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .AsNoTracking()
            .FirstAsync(x => x.UserId == userId, cancellationToken);

        var points = await _context.Set<UserEloScore>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.CurrentPoints)
            .FirstOrDefaultAsync(cancellationToken)
            ?? UserEloCalculator.DefaultPoints;

        var transactions = _context.Set<UserEloPointTransaction>()
            .AsNoTracking()
            .Where(x => x.UserId == userId);

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
