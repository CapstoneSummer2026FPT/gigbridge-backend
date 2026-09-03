using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.Internal;

/// <summary>
/// Writes and retires the freelancer's "rework the plan" requests. Both freelancer review gates
/// (pre-signature contract details and post-signature milestone review) funnel through here so the
/// client's plan editor has one place to read from, whichever gate bounced the contract back.
/// </summary>
internal static class ContractPlanChangeRequests
{
    /// <summary>
    /// Opens a request and retires any earlier open one on the same contract, so
    /// <see cref="GetOpenAsync"/> never has to choose between two live rows.
    /// </summary>
    public static async Task<ContractPlanChangeRequest> RecordAsync(
        IApplicationDbContext context,
        Guid contractId,
        Guid requestedByUserId,
        string reason,
        IReadOnlyCollection<Guid>? affectedMilestoneIds,
        IReadOnlyCollection<Guid>? affectedWorkItemIds,
        ContractPlanChangeOrigin origin,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await ResolveOpenAsync(context, contractId, now, cancellationToken);

        var request = new ContractPlanChangeRequest
        {
            ContractPlanChangeRequestId = Guid.NewGuid(),
            ContractsId = contractId,
            RequestedByUserId = requestedByUserId,
            Reason = reason,
            AffectedMilestoneIds = (affectedMilestoneIds ?? []).Distinct().ToArray(),
            AffectedWorkItemIds = (affectedWorkItemIds ?? []).Distinct().ToArray(),
            Origin = (int)origin,
            CreatedAt = now
        };

        context.Set<ContractPlanChangeRequest>().Add(request);
        return request;
    }

    /// <summary>Retires every open request on the contract. Called when the client resubmits.</summary>
    public static async Task ResolveOpenAsync(
        IApplicationDbContext context,
        Guid contractId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var open = await context.Set<ContractPlanChangeRequest>()
            .Where(request => request.ContractsId == contractId && request.ResolvedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var request in open)
        {
            request.ResolvedAt = now;
        }
    }

    public static Task<ContractPlanChangeRequest?> GetOpenAsync(
        IApplicationDbContext context,
        Guid contractId,
        CancellationToken cancellationToken) =>
        context.Set<ContractPlanChangeRequest>()
            .AsNoTracking()
            .Where(request => request.ContractsId == contractId && request.ResolvedAt == null)
            .OrderByDescending(request => request.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
