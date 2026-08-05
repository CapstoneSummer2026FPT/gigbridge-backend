using System;
using System.Collections.Generic;

namespace Application.Features.Elo.DTOs;

/// <summary>Attachment (file or description) submitted as part of an Elo appeal.</summary>
public sealed record EloAppealEvidenceDto(
    Guid EvidenceId,
    Guid AppealId,
    Guid UploadedById,
    string? FileName,
    string? FileUrl,
    long? FileSize,
    string? Description,
    DateTime CreatedAt);

/// <summary>Appeal row for list endpoints. Evidence is excluded until the detail view.</summary>
public sealed record EloAppealDto(
    Guid AppealId,
    Guid UserId,
    Guid TransactionId,
    int Status,
    int? Resolution,
    string Reason,
    string? ResolutionNote,
    int? CorrectedDelta,
    Guid? AppliedTransactionId,
    Guid? ReviewedByAdminId,
    DateTime? ReviewedAt,
    Guid? CancelledById,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Appeal detail: includes the attached evidence and the appealed transaction.</summary>
public sealed record EloAppealDetailDto(
    EloAppealDto Appeal,
    EloTransactionDto? Transaction,
    IReadOnlyList<EloAppealEvidenceDto> Evidence);
