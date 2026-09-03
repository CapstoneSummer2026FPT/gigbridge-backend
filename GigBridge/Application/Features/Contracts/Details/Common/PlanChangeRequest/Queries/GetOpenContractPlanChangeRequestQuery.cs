using Application.Features.Contracts.Details.Common.PlanChangeRequest.DTOs;
using MediatR;

namespace Application.Features.Contracts.Details.Common.PlanChangeRequest.Queries;

public sealed record GetOpenContractPlanChangeRequestQuery(Guid ContractId, Guid UserId)
    : IRequest<ContractPlanChangeRequestDto?>;
