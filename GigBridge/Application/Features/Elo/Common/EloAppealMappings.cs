using Application.Features.Elo.DTOs;
using Domain.Entities;

namespace Application.Features.Elo.Common;

public static class EloAppealMappings
{
    /// <summary>In-memory mapping (use an inline Select projection for EF queries).</summary>
    public static EloAppealDto ToDto(EloPointAppeal x) => new(
        x.EloPointAppealId, x.UserId, x.EloPointTransactionId, x.Status, x.Resolution,
        x.Reason, x.ResolutionNote, x.CorrectedDelta, x.AppliedTransactionId,
        x.ReviewedByAdminId, x.ReviewedAt, x.CancelledById, x.CancelledAt,
        x.CreatedAt, x.UpdatedAt);
}
