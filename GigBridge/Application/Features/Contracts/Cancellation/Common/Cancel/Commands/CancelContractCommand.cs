using Application.Features.Contracts.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Cancellation.Common.Cancel.Commands;

public sealed record CancelContractCommand(
    Guid ContractId,
    Guid UserId) : IRequest<ContractWorkflowResponse>;
