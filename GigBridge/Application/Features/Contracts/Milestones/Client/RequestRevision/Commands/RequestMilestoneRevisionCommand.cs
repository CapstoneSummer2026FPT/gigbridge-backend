using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Client.RequestRevision.DTOs;
using MediatR;

namespace Application.Features.Contracts.Milestones.Client.RequestRevision.Commands;

public sealed record RequestMilestoneRevisionCommand(
    Guid ContractId,
    Guid MilestoneId,
    Guid UserId,
    RequestMilestoneRevisionRequest? Request = null) : IRequest<ContractMilestoneResponse>;
