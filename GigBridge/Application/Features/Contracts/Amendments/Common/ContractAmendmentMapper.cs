using Application.Features.Contracts.Amendments.DTOs;
using Domain.Entities;

namespace Application.Features.Contracts.Amendments.Common;

internal static class ContractAmendmentMapper
{
    public static ContractAmendmentDetailDto ToDetail(ContractAmendment amendment)
    {
        return new ContractAmendmentDetailDto(
            amendment.ContractAmendmentId,
            amendment.ContractsId,
            amendment.ContractChangeRequestId,
            amendment.RevisionNumber,
            amendment.Reason,
            amendment.OriginalTotalBudget,
            amendment.ProposedTotalBudget,
            amendment.BudgetDelta,
            amendment.ReviewNote,
            amendment.Status,
            amendment.Signatures.Count,
            amendment.CreatedAt,
            amendment.AppliedAt,
            amendment.Milestones
                .OrderBy(item => item.OrderIndex)
                .Select(item => new ContractAmendmentMilestoneDto(
                    item.SourceMilestoneId,
                    item.Title,
                    item.Description,
                    item.Amount,
                    item.EstimatedDuration,
                    item.DueDate,
                    item.Deliverables,
                    item.AcceptanceCriteria,
                    item.OrderIndex,
                    item.WorkItems.OrderBy(workItem => workItem.OrderIndex)
                        .Select(workItem => new ContractAmendmentWorkItemDto(
                            workItem.SourceContractWorkItemId,
                            workItem.Title,
                            workItem.Description,
                            workItem.Deliverables,
                            workItem.EstimatedDuration,
                            workItem.OrderIndex))
                        .ToList()))
                .ToList());
    }
}
