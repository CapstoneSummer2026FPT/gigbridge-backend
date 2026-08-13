using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Notifications.Common.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Auditing;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Freelancer.RequestUnlock.Commands;

public sealed class RequestMilestoneUnlockCommandHandler :
    IRequestHandler<RequestMilestoneUnlockCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly IUserAuditLogService _userAuditLog;

    public RequestMilestoneUnlockCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        IUserAuditLogService userAuditLog)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _userAuditLog = userAuditLog;
    }

    public async Task Handle(
        RequestMilestoneUnlockCommand command,
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

        if (milestone.Status != (int)MilestoneStatus.Pending)
        {
            throw new BadRequestException("Only pending milestones can be requested for unlock.");
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            throw new BadRequestException("Early start reason is required.");
        }

        var milestones = await MilestoneWorkflowGuard.OrderMilestones(
                _context.Set<Milestone>().Where(item => item.ContractsId == contract.ContractsId))
            .ToListAsync(cancellationToken);
        var nextPending = milestones.FirstOrDefault(item => item.Status == (int)MilestoneStatus.Pending);
        if (nextPending?.MilestonesId != milestone.MilestonesId)
        {
            throw new BadRequestException("Only the next consecutive pending milestone can be requested for early start.");
        }
        var hasPendingRequest = await _context.Set<MilestoneEarlyStartRequest>().AnyAsync(
            item => item.MilestonesId == milestone.MilestonesId &&
                    item.Status == (int)MilestoneEarlyStartRequestStatus.Pending,
            cancellationToken);
        if (hasPendingRequest) throw new ConflictException("An early start request is already pending for this milestone.");

        var clientUserId = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (clientUserId == Guid.Empty)
        {
            throw new BadRequestException("Contract client profile is invalid.");
        }

        var now = _dateTimeService.UtcNow;
        contract.UpdatedAt = now;
        _context.Set<MilestoneEarlyStartRequest>().Add(new MilestoneEarlyStartRequest
        {
            MilestoneEarlyStartRequestId = Guid.NewGuid(),
            ContractsId = contract.ContractsId,
            MilestonesId = milestone.MilestonesId,
            RequestedByUserId = command.UserId,
            Reason = command.Reason.Trim(),
            Status = (int)MilestoneEarlyStartRequestStatus.Pending,
            CreatedAt = now
        });

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            $"Freelancer requested milestone unlock: {milestone.Title}.",
            now,
            cancellationToken);

        await _notificationService.CreateNotificationAsync(
            clientUserId,
            NotificationType.MilestoneUpdated,
            "Milestone unlock requested",
            $"Freelancer requested to unlock milestone '{milestone.Title}' for contract '{contract.Title}'.",
            milestone.MilestonesId,
            "Milestone",
            cancellationToken);

        _userAuditLog.Add(
            command.UserId,
            UserRole.Freelancer,
            AuditUserActionType.RequestedEarlyStart,
            contract.ContractsId,
            $"Requested early start on milestone: {milestone.Title}.",
            milestoneId: milestone.MilestonesId);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
