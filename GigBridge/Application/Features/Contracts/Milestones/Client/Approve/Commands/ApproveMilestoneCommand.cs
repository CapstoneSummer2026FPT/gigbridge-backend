using Application.Features.Contracts.Milestones.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Milestones.Client.Approve.Commands;

public sealed record ApproveMilestoneCommand(
    Guid ContractId,
    Guid MilestoneId,
    Guid UserId) : IRequest<ContractMilestoneResponse>;
