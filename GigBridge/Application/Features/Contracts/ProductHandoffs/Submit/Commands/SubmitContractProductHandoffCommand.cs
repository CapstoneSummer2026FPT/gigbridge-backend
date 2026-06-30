using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.ProductHandoffs.Submit.Commands;

public sealed record SubmitContractProductHandoffCommand(
    Guid ContractId,
    Guid UserId,
    SubmitContractProductHandoffFile? File,
    string? ExternalUrl,
    string? Note) : IRequest<ContractProductHandoffResponse>;
