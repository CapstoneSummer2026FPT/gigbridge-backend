using Application.Features.Contracts.Milestones.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Milestones.WorkItems.Client.Review.Commands;

/// <summary>
/// Approve or send back a batch of work items. Both verdicts share one command — and therefore one
/// reconciliation path — so "approve the last item" and "reject one item" can never disagree about
/// what the milestone status should become.
/// </summary>
/// <param name="Approve">true = approve the listed items; false = send them back for revision.</param>
public sealed record ReviewContractWorkItemsCommand(
    Guid ContractId,
    Guid MilestoneId,
    Guid UserId,
    IReadOnlyList<Guid> WorkItemIds,
    bool Approve,
    string? Reason) : IRequest<ReviewWorkItemsResponse>;
