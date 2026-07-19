using Application.Features.Admin.Disputes.Common.DTOs;
using Domain.Enums;
using MediatR;

namespace Application.Features.Admin.Disputes.Resolve.Commands;

public enum AdminContractAction
{
    Resume = 0,
    Terminate = 1
}

public sealed record AdminMilestoneDecisionInput(
    Guid MilestoneId,
    DisputeMilestoneOutcome Outcome,
    decimal AdditionalReleaseToFreelancer,
    decimal RefundToClient);

public sealed record ResolveAdminDisputeCommand(
    Guid DisputeId,
    Guid AdminId,
    DisputeResolution Resolution,
    string ResolutionNote,
    string? InternalNotes,
    IReadOnlyList<AdminMilestoneDecisionInput> MilestoneDecisions,
    AdminContractAction ContractAction) : IRequest<AdminDisputeDetailResponse>;
