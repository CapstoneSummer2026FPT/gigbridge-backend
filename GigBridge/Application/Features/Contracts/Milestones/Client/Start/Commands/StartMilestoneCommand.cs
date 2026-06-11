using Application.Features.Contracts.Milestones.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Milestones.Client.Start.Commands;

public sealed record StartMilestoneCommand(
    Guid ContractId,
    Guid MilestoneId,
    Guid UserId) : IRequest<ContractMilestoneResponse>;
