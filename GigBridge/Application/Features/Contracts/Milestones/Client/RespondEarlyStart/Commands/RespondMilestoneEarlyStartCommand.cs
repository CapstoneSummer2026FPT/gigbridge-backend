using Application.Features.Contracts.Milestones.Client.RespondEarlyStart.DTOs;
using Application.Features.Contracts.Milestones.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Milestones.Client.RespondEarlyStart.Commands;

public sealed record RespondMilestoneEarlyStartCommand(
    Guid ContractId,
    Guid RequestId,
    Guid UserId,
    RespondMilestoneEarlyStartRequest Request) : IRequest<MilestoneEarlyStartRequestDto>;
