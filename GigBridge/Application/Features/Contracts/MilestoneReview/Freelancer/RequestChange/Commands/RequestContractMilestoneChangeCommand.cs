using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Details.Freelancer.RequestChange.DTOs;
using MediatR;

namespace Application.Features.Contracts.MilestoneReview.Freelancer.RequestChange.Commands;

public sealed record RequestContractMilestoneChangeCommand(
    Guid ContractId,
    Guid UserId,
    RequestContractDetailsChangeRequest Request) : IRequest<ContractWorkflowResponse>;
