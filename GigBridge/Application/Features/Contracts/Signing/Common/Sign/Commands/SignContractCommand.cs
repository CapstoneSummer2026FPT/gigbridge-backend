using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Signing.Common.Sign.DTOs;
using MediatR;

namespace Application.Features.Contracts.Signing.Common.Sign.Commands;

public sealed record SignContractCommand(
    Guid ContractId,
    Guid UserId,
    SignContractRequest Request,
    string? IpAddress,
    string? UserAgent) : IRequest<ContractWorkflowResponse>;
