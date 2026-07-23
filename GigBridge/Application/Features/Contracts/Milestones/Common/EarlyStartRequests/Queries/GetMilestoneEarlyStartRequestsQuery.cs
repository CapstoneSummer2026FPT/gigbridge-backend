using Application.Features.Contracts.Milestones.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Milestones.Common.EarlyStartRequests.Queries;

public sealed record GetMilestoneEarlyStartRequestsQuery(Guid ContractId, Guid UserId)
    : IRequest<IReadOnlyList<MilestoneEarlyStartRequestDto>>;
