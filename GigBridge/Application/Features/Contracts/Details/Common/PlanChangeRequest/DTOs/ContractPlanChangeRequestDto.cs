namespace Application.Features.Contracts.Details.Common.PlanChangeRequest.DTOs;

/// <summary>
/// The open "rework the plan" request on a contract, or null when there is none. Drives the badge
/// the client sees on step 1 of the contract workspace.
/// </summary>
public sealed record ContractPlanChangeRequestDto(
    Guid ContractPlanChangeRequestId,
    Guid ContractId,
    Guid RequestedByUserId,
    string RequestedByName,
    string Reason,
    IReadOnlyList<Guid> AffectedMilestoneIds,
    IReadOnlyList<Guid> AffectedWorkItemIds,
    /// <summary>Enum ContractPlanChangeOrigin: 0=ContractDetails, 1=MilestoneReview.</summary>
    int Origin,
    DateTime CreatedAt);
