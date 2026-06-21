using Application.Features.Contracts.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Details.Freelancer.Confirm.Commands;

public sealed record ConfirmContractDetailsCommand(Guid ContractId, Guid UserId) : IRequest<ContractWorkflowResponse>;
