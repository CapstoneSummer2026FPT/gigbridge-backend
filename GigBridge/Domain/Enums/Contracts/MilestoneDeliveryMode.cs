namespace Domain.Enums.Contracts;

/// <summary>
/// How deliverables move on a contract. Persisted on <c>Contract.DeliveryMode</c> and frozen once the
/// contract becomes Active.
///
/// This is a stored value on purpose. Contracts created before the work-item delivery flow already carry
/// <c>ContractWorkItem</c> rows, so inferring the mode from the number of work items would silently move
/// every live contract onto endpoints its participants have never seen. The migration stamps every existing
/// contract <see cref="Legacy"/>; only contracts materialized by the new code start as <see cref="WorkItem"/>.
/// </summary>
public enum MilestoneDeliveryMode
{
    /// <summary>Freelancer submits one bundle per milestone; the client approves the milestone as a whole.</summary>
    Legacy = 0,

    /// <summary>Freelancer submits per work item; the client approves per work item and the milestone auto-closes.</summary>
    WorkItem = 1
}
