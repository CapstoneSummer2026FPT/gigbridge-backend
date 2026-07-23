using Application.Common.Exceptions;
using Domain.Entities;

namespace Application.Features.Contracts.Common.Internal;

internal static class ContractDetailsValidator
{

    public static void ValidateMilestoneDraft(IReadOnlyCollection<Milestone> milestones)
    {
        if (milestones.Any(milestone => string.IsNullOrWhiteSpace(milestone.Title)))
        {
            throw new BadRequestException("Milestone title is required.");
        }

        if (milestones.Any(milestone => milestone.Amount <= 0))
        {
            throw new BadRequestException("Milestone amount must be greater than zero.");
        }

        if (milestones.Any(milestone => milestone.WorkItems.Count == 0 ||
            milestone.WorkItems.Any(item =>
                string.IsNullOrWhiteSpace(item.Title) ||
                string.IsNullOrWhiteSpace(item.Description))))
        {
            throw new BadRequestException("Each milestone requires at least one titled and described work item.");
        }
    }

    public static void ValidateMilestonesForSubmitOrPublish(Contract contract, IReadOnlyCollection<Milestone> milestones)
    {
        if (milestones.Count == 0)
        {
            throw new BadRequestException("Contract details must include at least one milestone.");
        }

        ValidateMilestoneDraft(milestones);
        ValidateMilestoneTotalDoesNotExceedBudget(contract, milestones);

        var total = milestones.Sum(milestone => milestone.Amount);
        if (total != contract.TotalBudget)
        {
            throw new BadRequestException($"Milestone total sum ({total}) must equal contract total budget ({contract.TotalBudget}).");
        }
    }

    public static void ValidateMilestoneTotalDoesNotExceedBudget(Contract contract, IReadOnlyCollection<Milestone> milestones)
    {
        var total = milestones.Sum(milestone => milestone.Amount);
        if (total > contract.TotalBudget)
        {
            throw new BadRequestException($"Allocated milestone budget ({total}) cannot exceed contract total budget ({contract.TotalBudget}).");
        }
    }
}
