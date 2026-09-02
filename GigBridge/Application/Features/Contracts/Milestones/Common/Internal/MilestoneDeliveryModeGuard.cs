using Application.Common.Exceptions;
using Domain.Entities;
using Domain.Enums.Contracts;

namespace Application.Features.Contracts.Milestones.Common.Internal;

/// <summary>
/// Decides which delivery flow a contract is on, and refuses the endpoints belonging to the other one.
///
/// The mode is read from the persisted <see cref="Contract.DeliveryMode"/> and is never inferred from the
/// number of work items. Contracts created before the work-item flow already carry <see cref="ContractWorkItem"/>
/// rows, so counting them would silently move every live contract onto endpoints its participants have never
/// seen — and strand freelancers mid-milestone.
/// </summary>
internal static class MilestoneDeliveryModeGuard
{
    public static MilestoneDeliveryMode Resolve(Contract contract) =>
        (MilestoneDeliveryMode)contract.DeliveryMode;

    public static bool UsesWorkItems(Contract contract) =>
        Resolve(contract) == MilestoneDeliveryMode.WorkItem;

    /// <summary>Guards the per-work-item endpoints.</summary>
    public static void EnsureWorkItemDelivery(Contract contract)
    {
        if (!UsesWorkItems(contract))
        {
            throw new BadRequestException(
                "This contract delivers at milestone level. Submit and approve the milestone instead.");
        }
    }

    /// <summary>Guards the legacy milestone-level submit endpoint.</summary>
    public static void EnsureLegacySubmission(Contract contract)
    {
        if (UsesWorkItems(contract))
        {
            throw new BadRequestException(
                "This contract delivers per work item. Submit each work item from the delivery space.");
        }
    }

    /// <summary>Guards the legacy milestone-level approve endpoint.</summary>
    public static void EnsureLegacyApproval(Contract contract)
    {
        if (UsesWorkItems(contract))
        {
            throw new BadRequestException(
                "This contract delivers per work item. Approve each work item from the delivery space.");
        }
    }
}
