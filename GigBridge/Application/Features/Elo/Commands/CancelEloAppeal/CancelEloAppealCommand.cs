using Application.Features.Elo.DTOs;
using MediatR;

namespace Application.Features.Elo.Commands.CancelEloAppeal;

/// <summary>
/// Withdraws the user's own appeal while it is still Pending. Once an admin has
/// started reviewing (UnderReview) or resolved it, cancellation is not allowed.
/// </summary>
public sealed record CancelEloAppealCommand(
    Guid UserId,
    Guid AppealId) : IRequest<EloAppealDto>;
