using System;
using System.Collections.Generic;
using Application.Features.Elo.DTOs;

namespace Application.Features.Admin.Elo.DTOs;

/// <summary>Identity/contact subset of the target user shown beside admin Elo rows.</summary>
public sealed record AdminEloUserInfoDto(
    Guid UserId,
    string FullName,
    string? Avatar,
    string Email,
    int Role);

/// <summary>An Elo ledger row decorated with the owning user for admin browsing.</summary>
public sealed record AdminEloTransactionRowDto(
    Guid TransactionId,
    AdminEloUserInfoDto User,
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

/// <summary>Headline state + recent rows for a single user from the admin perspective.</summary>
public sealed record AdminEloUserSummaryDto(
    AdminEloUserInfoDto User,
    int CurrentPoints,
    int TotalGained,
    int TotalLost,
    int TotalTransactions,
    IReadOnlyList<EloTransactionDto> RecentTransactions);

/// <summary>An appeal row with the appealing user and (for resolved rows) the reviewing admin.</summary>
public sealed record AdminEloAppealRowDto(
    Guid AppealId,
    AdminEloUserInfoDto User,
    Guid TransactionId,
    int Status,
    int? Resolution,
    string Reason,
    string? ResolutionNote,
    int? CorrectedDelta,
    Guid? AppliedTransactionId,
    Guid? ReviewedByAdminId,
    string? ReviewedByAdminName,
    DateTime? ReviewedAt,
    Guid? CancelledById,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Admin appeal detail: the appeal plus its evidence, the appealed transaction,
/// and the appealing user's current score summary.
/// </summary>
public sealed record AdminEloAppealDetailDto(
    AdminEloAppealRowDto Appeal,
    EloTransactionDto? Transaction,
    IReadOnlyList<EloAppealEvidenceDto> Evidence,
    AdminEloUserSummaryDto UserSummary);
