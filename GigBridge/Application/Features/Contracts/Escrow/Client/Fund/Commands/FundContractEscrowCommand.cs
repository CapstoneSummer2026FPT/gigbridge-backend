using Application.Features.Contracts.Escrow.Client.Fund.DTOs;
using MediatR;

namespace Application.Features.Contracts.Escrow.Client.Fund.Commands;

public sealed record FundContractEscrowCommand(Guid ContractId, Guid UserId) : IRequest<FundContractEscrowResponse>;
