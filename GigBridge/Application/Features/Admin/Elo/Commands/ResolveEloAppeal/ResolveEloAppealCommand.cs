using Application.Features.Elo.DTOs;
using Domain.Enums.Elo;
using MediatR;

namespace Application.Features.Admin.Elo.Commands.ResolveEloAppeal;

/// <summary>
/// Resolves an Elo appeal. The target <see cref="Status"/> may be UnderReview
/// (marking the appeal as being reviewed) or a final decision
/// (Approved/PartiallyApproved/Rejected). Final decisions apply a correction via
/// the centralized ledger (idempotent per appeal) and are audited under
/// Elo.AppealResolution.
/// </summary>
public sealed record ResolveEloAppealCommand(
    Guid AdminId,
    Guid AppealId,
    EloPointAppealStatus Status,
    EloPointAppealResolution Resolution,
    int? CorrectedDelta,
    string? ResolutionNote) : IRequest<EloAppealDto>;
