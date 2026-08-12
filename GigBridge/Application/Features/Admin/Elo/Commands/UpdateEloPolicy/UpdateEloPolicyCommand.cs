using Application.Features.Elo.DTOs;
using Domain.Enums.Elo;
using MediatR;

namespace Application.Features.Admin.Elo.Commands.UpdateEloPolicy;

/// <summary>
/// Updates the platform-wide Elo policy (currently the dispute-resolution penalty
/// mode and value) stored in PlatformSetting rows. Audited under Elo.PolicyUpdate.
/// </summary>
public sealed record UpdateEloPolicyCommand(
    Guid AdminId,
    EloAdjustmentMode Mode,
    decimal Value) : IRequest<EloPolicyDto>;
