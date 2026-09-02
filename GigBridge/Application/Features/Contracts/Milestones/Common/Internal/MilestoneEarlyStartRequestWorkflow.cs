using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Contracts.Milestones;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Common.Internal;

/// <summary>
/// Keeps early-start requests consistent with the milestone they target.
/// A pending request becomes obsolete as soon as another workflow starts the milestone.
/// </summary>
internal static class MilestoneEarlyStartRequestWorkflow
{
    public const string AutomaticCancellationNote =
        "Automatically cancelled because the milestone started through the normal workflow.";

    public static void CancelAsSuperseded(MilestoneEarlyStartRequest request, DateTime now)
    {
        if (request.Status != (int)MilestoneEarlyStartRequestStatus.Pending)
        {
            return;
        }

        request.Status = (int)MilestoneEarlyStartRequestStatus.Cancelled;
        request.ResponseNote = AutomaticCancellationNote;
        request.RespondedByUserId = null;
        request.RespondedAt = now;
    }

    public static async Task CancelPendingForMilestoneAsync(
        IApplicationDbContext context,
        Guid milestoneId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var pendingRequests = await context.Set<MilestoneEarlyStartRequest>()
            .Where(request =>
                request.MilestonesId == milestoneId &&
                request.Status == (int)MilestoneEarlyStartRequestStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var request in pendingRequests)
        {
            CancelAsSuperseded(request, now);
        }
    }
}
