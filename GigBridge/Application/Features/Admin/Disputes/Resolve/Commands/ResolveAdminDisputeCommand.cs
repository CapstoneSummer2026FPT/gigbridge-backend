using Application.Features.Admin.Disputes.Common.DTOs;
using Domain.Enums.Accounts;
using Domain.Enums.Disputes;
using MediatR;

namespace Application.Features.Admin.Disputes.Resolve.Commands;

public enum AdminContractAction
{
    Resume = 0,
    Terminate = 1
}

public sealed record AdminMilestoneAllocationInput(
    Guid MilestoneId,
    DisputeMilestoneOutcome Outcome,
    decimal FreelancerAward,
    decimal ClientRefund,
    decimal PenaltyAmount,
    string? Reason);

public sealed record AdminViolationInput(
    bool IsViolation,
    UserViolationType? ViolationType,
    string? Reason,
    string? Description);

public sealed record ResolveAdminDisputeCommand(
    Guid DisputeId,
    Guid AdminId,
    DisputeResolution Resolution,
    string ResolutionNote,
    string? InternalNotes,
    IReadOnlyList<AdminMilestoneAllocationInput> MilestoneAllocations,
    AdminContractAction ContractAction,
    AdminViolationInput ClientViolation,
    AdminViolationInput FreelancerViolation) : IRequest<AdminDisputeDetailResponse>;
