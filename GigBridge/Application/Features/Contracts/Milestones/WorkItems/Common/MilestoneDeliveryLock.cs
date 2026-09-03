namespace Application.Features.Contracts.Milestones.WorkItems.Common;

/// <summary>
/// Advisory lock keys for work item delivery, scoped to a single milestone.
///
/// Deliberately not <c>ContractEscrowLock.ForContract</c>: that key guards money movement, and
/// reusing it would serialize every bulk approval against escrow funding and payouts for no reason.
/// A milestone-scoped key still makes one batch atomic against another batch on the same milestone —
/// which is what stops two concurrent submissions from both missing that the milestone just became
/// fully submitted — while leaving other milestones free to proceed.
/// </summary>
internal static class MilestoneDeliveryLock
{
    private const long Namespace = 0x574B49544D53544E; // "WKITMSTN"

    public static long ForMilestone(Guid milestoneId) =>
        BitConverter.ToInt64(milestoneId.ToByteArray(), 0) ^ Namespace;
}
