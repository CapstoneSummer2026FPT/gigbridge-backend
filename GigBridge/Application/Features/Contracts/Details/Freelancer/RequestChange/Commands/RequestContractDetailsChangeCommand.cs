using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Details.Freelancer.RequestChange.DTOs;
using MediatR;

namespace Application.Features.Contracts.Details.Freelancer.RequestChange.Commands;

public sealed record RequestContractDetailsChangeCommand(
    Guid ContractId,
    Guid UserId,
    RequestContractDetailsChangeRequest Request) : IRequest<ContractWorkflowResponse>;
