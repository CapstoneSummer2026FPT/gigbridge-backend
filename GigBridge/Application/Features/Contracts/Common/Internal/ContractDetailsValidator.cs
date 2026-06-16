using Application.Common.Exceptions;
using Domain.Entities;

namespace Application.Features.Contracts.Common.Internal;

internal static class ContractDetailsValidator
{

    public static void ValidateMilestones(Contract contract, IReadOnlyCollection<Milestone> milestones)
    {
        if (milestones.Count == 0)
        {
            throw new BadRequestException("Contract details must include at least one milestone.");
        }

        if (milestones.Any(milestone => string.IsNullOrWhiteSpace(milestone.Title)))
        {
            throw new BadRequestException("Milestone title is required.");
        }

        if (milestones.Any(milestone => milestone.Amount <= 0))
        {
            throw new BadRequestException("Milestone amount must be greater than zero.");
        }

        var total = milestones.Sum(milestone => milestone.Amount);
        if (total != contract.TotalBudget)
        {
            throw new BadRequestException("Milestone total must equal contract total budget.");
        }
    }
}
