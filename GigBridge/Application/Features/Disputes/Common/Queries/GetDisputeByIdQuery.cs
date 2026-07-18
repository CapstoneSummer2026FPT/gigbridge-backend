using Application.Features.Disputes.Common.DTOs;
using MediatR;

namespace Application.Features.Disputes.Common.Queries;

public sealed record GetDisputeByIdQuery(
    Guid ContractId,
    Guid DisputeId,
    Guid UserId) : IRequest<DisputeResponse>;
