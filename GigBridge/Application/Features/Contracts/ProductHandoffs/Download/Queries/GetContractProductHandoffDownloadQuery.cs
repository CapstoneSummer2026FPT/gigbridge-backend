using Application.Features.Contracts.ProductHandoffs.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.ProductHandoffs.Download.Queries;

public sealed record GetContractProductHandoffDownloadQuery(
    Guid ContractId,
    Guid HandoffId,
    Guid UserId) : IRequest<ProductHandoffDownloadResponse>;
