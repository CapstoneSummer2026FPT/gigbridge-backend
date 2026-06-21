using Application.Features.Contracts.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Details.Client.Submit.Commands;

public sealed record SubmitContractDetailsCommand(Guid ContractId, Guid UserId) : IRequest<ContractWorkflowResponse>;
