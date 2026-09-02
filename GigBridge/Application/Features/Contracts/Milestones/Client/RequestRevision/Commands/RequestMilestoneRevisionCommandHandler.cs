using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Client.RequestRevision.Commands;

public sealed class RequestMilestoneRevisionCommandHandler :
    IRequestHandler<RequestMilestoneRevisionCommand, ContractMilestoneResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier? _realtimeNotifier;

    public RequestMilestoneRevisionCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier? realtimeNotifier = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<ContractMilestoneResponse> Handle(
        RequestMilestoneRevisionCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(
            _context,
            command.ContractId,
            cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureClientAsync(
            _context,
            contract,
            command.UserId,
            cancellationToken);

        // Milestone-level revision only. On a work item contract this endpoint would set item
        // statuses without recording the verdict on their submission attempts, leaving an item
        // marked RevisionRequired whose latest attempt still reads as awaiting review — and
        // without ever running the reconciliation that reopens the milestone consistently.
        MilestoneDeliveryModeGuard.EnsureLegacyApproval(contract);

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(
            _context,
            command.ContractId,
            command.MilestoneId,
            cancellationToken);

        if (milestone.Status != (int)MilestoneStatus.Submitted)
        {
            throw new BadRequestException("Only submitted milestones can be returned for revision.");
        }

        if (command.Request is null || string.IsNullOrWhiteSpace(command.Request.Reason) || command.Request.WorkItemIds.Count == 0)
        {
            throw new BadRequestException("Revision reason and at least one affected work item are required.");
        }

        var workItems = await _context.Set<Domain.Entities.ContractWorkItem>()
            .Where(item => item.MilestonesId == milestone.MilestonesId && command.Request.WorkItemIds.Contains(item.ContractWorkItemId))
            .ToListAsync(cancellationToken);
        if (workItems.Count != command.Request.WorkItemIds.Distinct().Count())
        {
            throw new BadRequestException("One or more revision work items do not belong to this milestone.");
        }

        var now = _dateTimeService.UtcNow;
        milestone.Status = (int)MilestoneStatus.InProgress;
        milestone.SubmittedAt = null;
        milestone.UpdatedAt = now;
        milestone.SubmissionDescription = $"Revision requested: {command.Request.Reason.Trim()}";
        contract.UpdatedAt = now;
        foreach (var item in workItems)
        {
            item.Status = (int)ContractWorkItemStatus.RevisionRequired;
            item.CompletedAt = null;
            item.UpdatedAt = now;
        }

        var systemMessage = await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            $"Milestone revision requested: {milestone.Title}. {command.Request.Reason.Trim()}",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        if (_realtimeNotifier is not null)
        {
            var participantIds = await MilestoneWorkflowGuard.GetParticipantUserIdsAsync(_context, contract, cancellationToken);
            await _realtimeNotifier.SendUsersEventAsync(
                participantIds,
                "MilestoneStatusChanged",
                new { contractId = contract.ContractsId, milestoneId = milestone.MilestonesId, status = milestone.Status },
                cancellationToken);
            if (systemMessage is not null)
                await _realtimeNotifier.SendConversationEventAsync(
                    systemMessage.ConversationsId, "ReceiveMessage",
                    ContractConversationEvents.ToRealtimePayload(systemMessage), cancellationToken);
        }

        return MilestoneWorkflowGuard.ToResponse(milestone, contract.DeliveryMode);
    }
}
