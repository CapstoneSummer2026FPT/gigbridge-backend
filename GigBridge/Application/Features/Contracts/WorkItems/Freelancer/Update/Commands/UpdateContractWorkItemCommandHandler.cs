using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.WorkItems.Freelancer.Update.Commands;

public sealed class UpdateContractWorkItemCommandHandler
    : IRequestHandler<UpdateContractWorkItemCommand, ContractWorkItemResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly IChatRealtimeNotifier? _realtimeNotifier;

    public UpdateContractWorkItemCommandHandler(
        IApplicationDbContext context, IDateTimeService clock, IChatRealtimeNotifier? realtimeNotifier = null)
    {
        _context = context;
        _clock = clock;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<ContractWorkItemResponse> Handle(UpdateContractWorkItemCommand command, CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(_context, command.ContractId, cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureFreelancerAsync(_context, contract, command.UserId, cancellationToken);

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(_context, command.ContractId, command.MilestoneId, cancellationToken);
        if (milestone.Status != (int)MilestoneStatus.InProgress && milestone.Status != (int)MilestoneStatus.Pending)
            throw new BadRequestException("Work items can only be updated while their milestone is in progress or pending.");

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
        if (milestone.Status == (int)MilestoneStatus.Pending)
        {
            var milestones = await MilestoneWorkflowGuard.OrderMilestones(
                    _context.Set<Milestone>().Where(m => m.ContractsId == contract.ContractsId))
                .ToListAsync(cancellationToken);

            var hasApprovedEarlyStart = await _context.Set<MilestoneEarlyStartRequest>().AnyAsync(
                request => request.MilestonesId == milestone.MilestonesId &&
                           request.Status == (int)MilestoneEarlyStartRequestStatus.Approved,
                cancellationToken);

            if (!MilestoneWorkflowGuard.IsEligibleToStart(milestone, milestones, hasApprovedEarlyStart))
            {
                var currentIndex = milestones.FindIndex(m => m.MilestonesId == milestone.MilestonesId);
                var previousTitle = currentIndex > 0 ? milestones[currentIndex - 1].Title : null;
                throw new BadRequestException(
                    previousTitle is not null
                        ? $"Cannot start work on milestone '{milestone.Title}' until previous milestone '{previousTitle}' is approved, or an early start request is approved."
                        : $"Cannot start work on milestone '{milestone.Title}' yet.");
            }

            milestone.Status = (int)MilestoneStatus.InProgress;
            milestone.StartedAt ??= now;
        }

        item.Status = (int)next;
        item.ProgressNote = string.IsNullOrWhiteSpace(command.Request.ProgressNote) ? null : command.Request.ProgressNote.Trim();
        item.CompletedAt = next == ContractWorkItemStatus.Completed ? now : null;
        item.UpdatedAt = now;
        milestone.UpdatedAt = now;
        contract.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        if (_realtimeNotifier is not null)
        {
            var participantIds = await MilestoneWorkflowGuard.GetParticipantUserIdsAsync(_context, contract, cancellationToken);
            await _realtimeNotifier.SendUsersEventAsync(
                participantIds,
                "WorkItemUpdated",
                new
                {
                    contractId = contract.ContractsId, milestoneId = milestone.MilestonesId,
                    workItemId = item.ContractWorkItemId, status = item.Status, milestoneStatus = milestone.Status
                },
                cancellationToken);
        }

        return new ContractWorkItemResponse(item.ContractWorkItemId, item.MilestonesId, item.Title, item.Description,
            item.Deliverables, item.EstimatedDuration, item.OrderIndex, item.Status, item.ProgressNote,
            item.CompletedAt, item.UpdatedAt);
    }
}
