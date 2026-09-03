namespace Domain.Enums.Contracts;

/// <summary>Which freelancer review gate produced a <see cref="Domain.Entities.ContractPlanChangeRequest"/>.</summary>
public enum ContractPlanChangeOrigin
{
    /// <summary>Freelancer reviewed the submitted plan before signing.</summary>
    ContractDetails = 0,

    /// <summary>Freelancer bounced the milestones back after signing, before escrow funding.</summary>
    MilestoneReview = 1
}
