using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Email;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Contracts.Interfaces;
using Application.Common.InternalServices.Contracts.Models;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Common.Models.Email;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Features.Contracts.Details.Freelancer.RequestChange.Commands;

public sealed class RequestContractDetailsChangeCommandHandler :
    IRequestHandler<RequestContractDetailsChangeCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IContractPlanChangeEmailRenderer _emailRenderer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RequestContractDetailsChangeCommandHandler> _logger;

    public RequestContractDetailsChangeCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        INotificationService notificationService,
        IEmailService emailService,
        IContractPlanChangeEmailRenderer emailRenderer,
        IConfiguration configuration,
        ILogger<RequestContractDetailsChangeCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _notificationService = notificationService;
        _emailService = emailService;
        _emailRenderer = emailRenderer;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ContractWorkflowResponse> Handle(
        RequestContractDetailsChangeCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        if (contract.Status != (int)ContractStatus.PendingContractConfirmation)
        {
            throw new BadRequestException("Only submitted contract details can receive change requests.");
        }

        var freelancerProfile = await ContractParticipantGuard.EnsureFreelancerAsync(
            _context,
            contract,
            command.UserId,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Request.Reason))
        {
            throw new BadRequestException("A reason is required when requesting contract plan changes.");
        }

        var reason = command.Request.Reason.Trim();
        var clientUserId = await _context.Set<ClientProfile>()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (clientUserId == Guid.Empty)
        {
            throw new BadRequestException("Contract client does not exist.");
        }

        var milestoneIds = await _context.Set<Milestone>()
            .Where(item => item.ContractsId == contract.ContractsId)
            .Select(item => item.MilestonesId)
            .ToListAsync(cancellationToken);
        if ((command.Request.AffectedMilestoneIds ?? []).Except(milestoneIds).Any())
        {
            throw new BadRequestException("One or more affected milestones do not belong to this contract.");
        }
        var workItemIds = await _context.Set<ContractWorkItem>()
            .Where(item => milestoneIds.Contains(item.MilestonesId))
            .Select(item => item.ContractWorkItemId)
            .ToListAsync(cancellationToken);
        if ((command.Request.AffectedWorkItemIds ?? []).Except(workItemIds).Any())
        {
            throw new BadRequestException("One or more affected work items do not belong to this contract.");
        }

        var now = _dateTimeService.UtcNow;
        contract.Status = (int)ContractStatus.PendingContractDetails;
        contract.UpdatedAt = now;

        await ContractPlanChangeRequests.RecordAsync(
            _context,
            contract.ContractsId,
            command.UserId,
            reason,
            command.Request.AffectedMilestoneIds,
            command.Request.AffectedWorkItemIds,
            ContractPlanChangeOrigin.ContractDetails,
            now,
            cancellationToken);

        var message = $"Contract plan changes requested: {reason}" +
            $" (milestones: {(command.Request.AffectedMilestoneIds ?? []).Count}, work items: {(command.Request.AffectedWorkItemIds ?? []).Count}).";

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            message,
            now,
            cancellationToken);

        await _notificationService.CreateNotificationAsync(
            clientUserId,
            NotificationType.MilestoneUpdated,
            "Project plan changes requested",
            $"The freelancer requested changes to the project plan for \"{contract.Title}\": {reason}",
            contract.ContractsId,
            "Contract",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await ContractPlanChangeEmails.SendToClientAsync(
            _context,
            _emailService,
            _emailRenderer,
            _configuration,
            _logger,
            contract,
            clientUserId,
            freelancerProfile.UserId,
            reason,
            cancellationToken);

        var participantUserIds = await _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(p => p.Conversations.ContractsId == contract.ContractsId && p.LeftAt == null && p.DeletedAt == null)
            .Select(p => p.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (participantUserIds.Any())
        {
            await _chatRealtimeNotifier.SendUsersEventAsync(
                [.. participantUserIds],
                "ContractDetailsChangeRequested",
                new { contractId = contract.ContractsId },
                cancellationToken);
        }

        return new ContractWorkflowResponse(contract.ContractsId, contract.Status, null, null);
    }
}
