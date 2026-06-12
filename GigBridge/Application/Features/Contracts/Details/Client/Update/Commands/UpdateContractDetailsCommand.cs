using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Details.Client.Update.DTOs;
using MediatR;

namespace Application.Features.Contracts.Details.Client.Update.Commands;

public sealed record UpdateContractDetailsCommand(
    Guid ContractId,
    Guid UserId,
    UpdateContractDetailsRequest Request) : IRequest<ContractWorkflowResponse>;
