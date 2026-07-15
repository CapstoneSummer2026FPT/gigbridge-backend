using Application.Features.Disputes.Common.DTOs;

namespace Application.Features.Admin.Disputes.Common.DTOs;

public sealed record AdminDisputePartyResponse(
    Guid UserId,
    Guid ProfileId,
    string FullName,
    string Email);

public sealed record AdminDisputeListItemResponse(
    Guid DisputeId,
    Guid ContractId,
    string ContractTitle,
    string InitiatorName,
    string? InitiatorRole,
    string ClientName,
    string? FreelancerName,
    Guid? MilestoneId,
    string? MilestoneTitle,
    string Reason,
    int Status,
    int? Resolution,
    string? ResolutionLabel,
    int EvidenceCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ResolvedAt);

public sealed record AdminDisputeListResponse(
    IReadOnlyList<AdminDisputeListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record AdminDisputeDetailResponse(
    Guid DisputeId,
    Guid ContractId,
    string ContractTitle,
    int ContractStatus,
    Guid InitiatorId,
    string InitiatorName,
    string? InitiatorRole,
    AdminDisputePartyResponse Client,
    AdminDisputePartyResponse? Freelancer,
    Guid? MilestoneId,
    string? MilestoneTitle,
    string Reason,
    int Status,
    int? Resolution,
    string? ResolutionLabel,
    string? ResolutionNote,
    Guid? ResolvedByAdminId,
    DateTime? ResolvedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<DisputeEvidenceResponse> Evidence);
