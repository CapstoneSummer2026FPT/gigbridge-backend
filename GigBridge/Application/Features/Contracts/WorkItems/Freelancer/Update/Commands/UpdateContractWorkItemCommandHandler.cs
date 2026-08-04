using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.WorkItems.Freelancer.Update.Commands;

public sealed class UpdateContractWorkItemCommandHandler
    : IRequestHandler<UpdateContractWorkItemCommand, ContractWorkItemResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;

    public UpdateContractWorkItemCommandHandler(IApplicationDbContext context, IDateTimeService clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<ContractWorkItemResponse> Handle(UpdateContractWorkItemCommand command, CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, command.ContractId, cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureFreelancerAsync(_context, contract, command.UserId, cancellationToken);

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(_context, command.ContractId, command.MilestoneId, cancellationToken);
        if (milestone.Status != (int)MilestoneStatus.InProgress)
            throw new BadRequestException("Work items can only be updated while their milestone is in progress.");

        var item = await _context.Set<ContractWorkItem>().FirstOrDefaultAsync(
            workItem => workItem.ContractWorkItemId == command.WorkItemId && workItem.MilestonesId == command.MilestoneId,
            cancellationToken) ?? throw new NotFoundException("Contract work item does not exist.");

        var next = (ContractWorkItemStatus)command.Request.Status;
        var current = (ContractWorkItemStatus)item.Status;
        var valid = (current, next) switch
        {
            (ContractWorkItemStatus.Todo, ContractWorkItemStatus.InProgress) => true,
            (ContractWorkItemStatus.InProgress, ContractWorkItemStatus.Completed) => true,
            (ContractWorkItemStatus.RevisionRequired, ContractWorkItemStatus.InProgress) => true,
            _ when current == next => true,
            _ => false
        };
        if (!valid) throw new BadRequestException($"Work item cannot transition from {current} to {next}.");

        var now = _clock.UtcNow;
        item.Status = (int)next;
        item.ProgressNote = string.IsNullOrWhiteSpace(command.Request.ProgressNote) ? null : command.Request.ProgressNote.Trim();
        item.CompletedAt = next == ContractWorkItemStatus.Completed ? now : null;
        item.UpdatedAt = now;
        contract.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        return new ContractWorkItemResponse(item.ContractWorkItemId, item.MilestonesId, item.Title, item.Description,
            item.Deliverables, item.EstimatedDuration, item.OrderIndex, item.Status, item.ProgressNote,
            item.CompletedAt, item.UpdatedAt);
    }
}
