namespace Domain.Entities;

/// <summary>
/// A freelancer's request for the client to rework the project plan while the contract is still
/// being negotiated. Distinct from <see cref="ContractChangeRequest"/>, which covers amendments to
/// an already-Active contract: this one is what bounces a contract back to PendingContractDetails,
/// and it is what the client's plan editor reads to explain why it reopened.
/// </summary>
public sealed class ContractPlanChangeRequest
{
    public Guid ContractPlanChangeRequestId { get; set; }

    public Guid ContractsId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public string Reason { get; set; } = null!;

    public Guid[] AffectedMilestoneIds { get; set; } = [];

    public Guid[] AffectedWorkItemIds { get; set; } = [];

    /// <summary>
    /// Enum ContractPlanChangeOrigin: 0=ContractDetails, 1=MilestoneReview.
    /// Records which review gate the freelancer bounced the plan back from.
    /// </summary>
    public int Origin { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Set when the client resubmits the plan, which is what retires the banner.</summary>
    public DateTime? ResolvedAt { get; set; }

    public Contract Contract { get; set; } = null!;
}
