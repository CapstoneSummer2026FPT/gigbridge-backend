namespace Application.Features.Disputes.Common.DTOs;

public sealed record CreateDisputeRequest(Guid ContractId, Guid? MilestoneId, string Reason);

public sealed record DisputeDto(
    Guid DisputeId,
    Guid ContractId,
    Guid InitiatorId,
    Guid? MilestoneId,
    string Reason,
    int Status,
    int? Resolution,
    string? ResolutionNote,
    bool IsVipPriority,
    DateTime? ResolutionTargetAt,
    string AiAnalysisStatus,
    DateTime CreatedAt);
