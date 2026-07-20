using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.WorkItems.Freelancer.Update.DTOs;
using MediatR;

namespace Application.Features.Contracts.WorkItems.Freelancer.Update.Commands;

public sealed record UpdateContractWorkItemCommand(
    Guid ContractId,
    Guid MilestoneId,
    Guid WorkItemId,
    Guid UserId,
    UpdateContractWorkItemRequest Request) : IRequest<ContractWorkItemResponse>;
