namespace Application.Features.ReportContracts.Common.DTOs;

public sealed record ReportContractListResponse(
    Guid ReportContractId,
    Guid ReporterId,
    string? ReporterName,
    string? ReporterRole,
    int IssueType,
    int Status,
    int? ResolutionAction,
    DateTime CreatedAt,
    DateTime? RespondedAt,
    DateTime? ResolvedAt);
