using Application.Features.Elo.DTOs;
using Domain.Enums;
using MediatR;

namespace Application.Features.Admin.Elo.Commands.ApplyAdminEloAdjustment;

/// <summary>
/// Manually adjusts a user's Elo points (increase/decrease) in either FixedPoints
/// or Percentage mode. Idempotent per <see cref="RequestId"/> so a client retry
/// cannot double-apply; the correction flows through the centralized ledger.
/// </summary>
public sealed record ApplyAdminEloAdjustmentCommand(
    Guid AdminId,
    Guid UserId,
    bool Increase,
    EloAdjustmentMode Mode,
    decimal Amount,
    string? Reason,
    Guid RequestId) : IRequest<EloTransactionDto?>;
