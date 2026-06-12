using Application.Features.Contracts.Milestones.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Milestones.Common.List.Queries;

public sealed record GetContractMilestonesQuery(
    Guid ContractId,
    Guid UserId) : IRequest<IReadOnlyList<ContractMilestoneResponse>>;
