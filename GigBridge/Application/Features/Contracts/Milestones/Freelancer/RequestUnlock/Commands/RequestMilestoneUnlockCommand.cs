using MediatR;

namespace Application.Features.Contracts.Milestones.Freelancer.RequestUnlock.Commands;

public sealed record RequestMilestoneUnlockCommand(
    Guid ContractId,
    Guid MilestoneId,
    Guid UserId) : IRequest;
