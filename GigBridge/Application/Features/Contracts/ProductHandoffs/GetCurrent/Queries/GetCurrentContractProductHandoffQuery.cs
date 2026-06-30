using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.ProductHandoffs.GetCurrent.Queries;

public sealed record GetCurrentContractProductHandoffQuery(
    Guid ContractId,
    Guid UserId) : IRequest<ContractProductHandoffResponse?>;
