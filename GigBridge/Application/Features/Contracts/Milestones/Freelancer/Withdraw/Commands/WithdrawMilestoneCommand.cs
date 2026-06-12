using Application.Features.Contracts.Milestones.Common.DTOs;
using MediatR;

namespace Application.Features.Contracts.Milestones.Freelancer.Withdraw.Commands;

public sealed record WithdrawMilestoneCommand(
    Guid ContractId,
    Guid MilestoneId,
    Guid UserId) : IRequest<WithdrawMilestoneResponse>;
