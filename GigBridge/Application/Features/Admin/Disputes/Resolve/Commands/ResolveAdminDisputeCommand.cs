using Application.Features.Admin.Disputes.Common.DTOs;
using Domain.Enums;
using MediatR;

namespace Application.Features.Admin.Disputes.Resolve.Commands;

public enum AdminContractAction
{
    Resume = 0,
    Terminate = 1
}

public sealed record AdminMilestoneAction(
    Guid MilestoneId,
    int Action); // 0=Approve, 1=Reject, 2=Cancel

public sealed record ResolveAdminDisputeCommand(
    Guid DisputeId,
    Guid AdminId,
    DisputeResolution Resolution,
    string ResolutionNote,
    string? InternalNotes,
    decimal? RefundToClientAmount,
    decimal? ReleaseToFreelancerAmount,
    IReadOnlyList<AdminMilestoneAction>? MilestoneActions,
    AdminContractAction ContractAction) : IRequest<AdminDisputeDetailResponse>;
