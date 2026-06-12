using Application.Features.Contracts.Milestones.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;

public sealed record SubmitMilestoneCommand(
    Guid ContractId,
    Guid MilestoneId,
    Guid UserId) : IRequest<ContractMilestoneResponse>;
