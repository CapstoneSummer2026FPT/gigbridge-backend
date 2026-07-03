using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Freelancer.RequestUnlock.Commands;

public sealed class RequestMilestoneUnlockCommandHandler :
    IRequestHandler<RequestMilestoneUnlockCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;

    public RequestMilestoneUnlockCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
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

        await _context.SaveChangesAsync(cancellationToken);
    }
}
