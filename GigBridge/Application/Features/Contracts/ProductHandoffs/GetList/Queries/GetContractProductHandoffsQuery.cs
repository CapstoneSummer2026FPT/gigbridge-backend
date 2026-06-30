using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.ProductHandoffs.GetList.Queries;

public sealed record GetContractProductHandoffsQuery(
    Guid ContractId,
    Guid UserId) : IRequest<IReadOnlyList<ContractProductHandoffResponse>>;
