namespace Application.Features.Disputes.Common.DTOs;

public sealed record MyDisputeSummaryResponse(
    Guid DisputeId,
    Guid ContractId,
    Guid JobPostId,
    string ProjectName,
    DateTime CreatedAt,
    int Status,
    Guid? MilestoneId,
    string? MilestoneTitle);

public sealed record MyDisputesResponse(
    IReadOnlyList<MyDisputeSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalItems);
