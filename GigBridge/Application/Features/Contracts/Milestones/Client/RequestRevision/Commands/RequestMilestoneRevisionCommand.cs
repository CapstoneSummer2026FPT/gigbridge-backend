using Application.Features.Contracts.Milestones.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Milestones.Client.RequestRevision.Commands;

public sealed record RequestMilestoneRevisionCommand(
    Guid ContractId,
    Guid MilestoneId,
    Guid UserId) : IRequest<ContractMilestoneResponse>;
