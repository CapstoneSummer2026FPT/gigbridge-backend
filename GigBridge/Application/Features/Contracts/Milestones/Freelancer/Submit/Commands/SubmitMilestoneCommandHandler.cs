using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Enums;
using MediatR;

namespace Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;

public sealed class SubmitMilestoneCommandHandler :
    IRequestHandler<SubmitMilestoneCommand, ContractMilestoneResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public SubmitMilestoneCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<ContractMilestoneResponse> Handle(
        SubmitMilestoneCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(
            _context,
            command.ContractId,
            cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureFreelancerAsync(
            _context,
            contract,
            command.UserId,
            cancellationToken);

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(
            _context,
            command.ContractId,
            command.MilestoneId,
            cancellationToken);

        if (milestone.Status != (int)MilestoneStatus.InProgress)
        {
            throw new BadRequestException("Only in-progress milestones can be submitted.");
        }

        var now = _dateTimeService.UtcNow;
        milestone.Status = (int)MilestoneStatus.Submitted;
        milestone.SubmittedAt = now;
        milestone.UpdatedAt = now;
        contract.UpdatedAt = now;

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            $"Milestone submitted: {milestone.Title}.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return MilestoneWorkflowGuard.ToResponse(milestone);
    }
}
