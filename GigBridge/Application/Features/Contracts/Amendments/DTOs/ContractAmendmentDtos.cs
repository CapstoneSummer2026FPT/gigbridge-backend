using Application.Features.Contracts.Details.Client.Update.DTOs;

namespace Application.Features.Contracts.Amendments.DTOs;

public sealed record CreateContractChangeRequest(
    string Reason,
    string RequestedChanges,
    IReadOnlyCollection<Guid> AffectedMilestoneIds,
    IReadOnlyCollection<Guid> AffectedWorkItemIds);

public sealed record RespondContractChangeRequest(bool Accept, bool NeedsClarification, string? Note);

public sealed record CreateContractAmendmentRequest(
    Guid ChangeRequestId,
    string Reason,
    IReadOnlyList<ContractMilestoneRequest> Milestones);

public sealed record RespondContractAmendmentRequest(bool Accept, bool RequestChanges, string? Note);

public sealed record SignContractAmendmentRequest(string SignatureData);

public sealed record ContractChangeRequestDto(
    Guid ChangeRequestId,
    Guid ContractId,
    Guid RequestedByUserId,
    string Reason,
    string RequestedChanges,
    string? ResponseNote,
    string? ClarificationRequestNote,
    string? ClarificationResponseNote,
    IReadOnlyCollection<Guid> AffectedMilestoneIds,
    IReadOnlyCollection<Guid> AffectedWorkItemIds,
    int Status,
    DateTime CreatedAt,
    DateTime? RespondedAt,
    DateTime? ClarifiedAt,
    bool CanRespond,
    bool CanClarify);

public sealed record ContractAmendmentWorkItemDto(
    Guid? SourceWorkItemId,
    string Title,
    string? Description,
    string? Deliverables,
    string? EstimatedDuration,
    int OrderIndex);

public sealed record ContractAmendmentMilestoneDto(
    Guid? SourceMilestoneId,
    string Title,
    string? Description,
    decimal Amount,
    string? EstimatedDuration,
    DateOnly? DueDate,
    string? Deliverables,
    string? AcceptanceCriteria,
    int OrderIndex,
    IReadOnlyList<ContractAmendmentWorkItemDto> WorkItems);

public sealed record ContractAmendmentDetailDto(
    Guid AmendmentId,
    Guid ContractId,
    Guid ChangeRequestId,
    int RevisionNumber,
    string Reason,
    decimal OriginalTotalBudget,
    decimal ProposedTotalBudget,
    decimal BudgetDelta,
    string? ReviewNote,
    int Status,
    int SignatureCount,
    DateTime CreatedAt,
    DateTime? AppliedAt,
    IReadOnlyList<ContractAmendmentMilestoneDto> Milestones);
