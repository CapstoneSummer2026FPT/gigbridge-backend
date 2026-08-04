using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.ProductHandoffs.Acknowledge.Commands;

public sealed record AcknowledgeContractProductHandoffCommand(
    Guid ContractId,
    Guid HandoffId,
    Guid UserId) : IRequest<ContractProductHandoffResponse>;
