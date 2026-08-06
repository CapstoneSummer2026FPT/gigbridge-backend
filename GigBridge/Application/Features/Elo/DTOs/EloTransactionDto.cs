using System;

namespace Application.Features.Elo.DTOs;

/// <summary>
/// Immutable view of a single Elo ledger transaction for the user-facing
/// history/summary endpoints. Mirrors <see cref="Domain.Entities.UserEloPointTransaction"/>
/// with enums kept as raw ints so the UI owns the display mapping.
/// </summary>
public sealed record EloTransactionDto(
    Guid TransactionId,
    Guid UserId,
    int PointsDelta,
    int PointsBefore,
    int PointsAfter,
    int Reason,
    int? SourceType,
    int? Mode,
    string? SourceEntityType,
    Guid? SourceEntityId,
    Guid? ContractId,
    Guid? ReviewId,
    decimal? Rating,
    Guid? EloAppealId,
    Guid? AppliedByAdminId,
    DateTime CreatedAt);
