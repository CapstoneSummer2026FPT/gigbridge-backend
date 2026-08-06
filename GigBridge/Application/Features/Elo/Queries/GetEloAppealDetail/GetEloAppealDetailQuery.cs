using Application.Features.Elo.DTOs;
using MediatR;

namespace Application.Features.Elo.Queries.GetEloAppealDetail;

/// <summary>
/// Loads an appeal owned by <see cref="UserId"/>, including its evidence and the
/// appealed transaction. The controller resolves the current user so ownership is
/// enforced before the handler runs.
/// </summary>
public sealed record GetEloAppealDetailQuery(
    Guid AppealId,
    Guid UserId) : IRequest<EloAppealDetailDto>;
