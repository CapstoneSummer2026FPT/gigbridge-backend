using Domain.Enums;

namespace Application.Features.Disputes.Common.DTOs;

public sealed record DisputeResponse(
    Guid DisputesId,
    Guid ContractsId,
    Guid InitiatorId,
    string? InitiatorName,
    string? InitiatorRole,
    Guid? MilestonesId,
    string? MilestoneTitle,
    string Reason,
    int Status,
    int? Resolution,
    string? ResolutionLabel,
    string? ResolutionNote,
    DateTime? ResolvedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<DisputeEvidenceResponse> Evidences);
