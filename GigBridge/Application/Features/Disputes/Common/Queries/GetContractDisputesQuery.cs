using Application.Features.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Disputes.Common.Queries;

public sealed record GetContractDisputesQuery(
    Guid ContractId,
    Guid UserId) : IRequest<IReadOnlyList<DisputeResponse>>;
