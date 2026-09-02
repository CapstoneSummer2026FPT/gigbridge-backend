using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Auditing;
using Domain.Enums.Contracts.Milestones;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Client.Approve.Commands;

public sealed class ApproveMilestoneCommandHandler :
    IRequestHandler<ApproveMilestoneCommand, ContractMilestoneResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IUserAuditLogService _userAuditLog;
    private readonly IChatRealtimeNotifier? _realtimeNotifier;

    public ApproveMilestoneCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IUserAuditLogService userAuditLog,
        IChatRealtimeNotifier? realtimeNotifier = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _userAuditLog = userAuditLog;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<ContractMilestoneResponse> Handle(
        ApproveMilestoneCommand command,
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

        // Milestone-level approval only. A work item contract closes its milestone automatically
        // once the last work item is approved, so this endpoint would race that reconciliation.
        MilestoneDeliveryModeGuard.EnsureLegacyApproval(contract);

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(
            _context,
            command.ContractId,
            command.MilestoneId,
            cancellationToken);

        if (milestone.Status == (int)MilestoneStatus.Approved && milestone.ReleasedAmount >= milestone.Amount)
        {
            return MilestoneWorkflowGuard.ToResponse(milestone, contract.DeliveryMode);
        }

        if (milestone.Status != (int)MilestoneStatus.Submitted)
        {
            throw new BadRequestException("Only submitted milestones can be approved.");
        }

        var now = _dateTimeService.UtcNow;
        milestone.Status = (int)MilestoneStatus.Approved;
        milestone.ApprovedAt = now;
        milestone.UpdatedAt = now;
        contract.UpdatedAt = now;

        var milestones = await MilestoneWorkflowGuard.OrderMilestones(
                _context.Set<Milestone>().Where(item => item.ContractsId == contract.ContractsId))
            .ToListAsync(cancellationToken);
        var nextMilestone = MilestoneWorkflowGuard.AdvanceNextMilestone(milestones, now);
        if (nextMilestone is not null)
        {
            await MilestoneEarlyStartRequestWorkflow.CancelPendingForMilestoneAsync(
                _context,
                nextMilestone.MilestonesId,
                now,
                cancellationToken);
        }

        var systemMessage = await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            $"Milestone approved: {milestone.Title}.",
            now,
            cancellationToken);

        _userAuditLog.Add(
            command.UserId,
            UserRole.Client,
            AuditUserActionType.MilestoneApproved,
            contract.ContractsId,
            $"Approved milestone: {milestone.Title}.",
            milestoneId: milestone.MilestonesId);

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
