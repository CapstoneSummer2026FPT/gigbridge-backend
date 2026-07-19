using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Admin.Disputes.Common.Internal;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Admin.Disputes.Resolve.Commands;

public sealed class ResolveAdminDisputeCommandHandler :
    IRequestHandler<ResolveAdminDisputeCommand, AdminDisputeDetailResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly INotificationService _notifications;
    private readonly ILogger<ResolveAdminDisputeCommandHandler> _logger;

    public ResolveAdminDisputeCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        INotificationService notifications,
        ILogger<ResolveAdminDisputeCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<AdminDisputeDetailResponse> Handle(
        ResolveAdminDisputeCommand command,
        CancellationToken cancellationToken)
    {
        await AdminDisputeSupport.EnsureAdminAsync(_context, command.AdminId, cancellationToken);

        if (!Enum.IsDefined(command.Resolution))
            throw new BadRequestException("Invalid dispute resolution.");

        if (string.IsNullOrWhiteSpace(command.ResolutionNote))
            throw new BadRequestException("Resolution note is required.");

        var dispute = await _context.Set<Dispute>()
            .Include(d => d.Contracts)
                .ThenInclude(c => c.ClientProfiles)
                    .ThenInclude(p => p.User)
            .Include(d => d.Contracts)
                .ThenInclude(c => c.FreelancerProfiles)
                    .ThenInclude(p => p!.User)
            .Include(d => d.Contracts)
                .ThenInclude(c => c.Milestones)
            .FirstOrDefaultAsync(d => d.DisputesId == command.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        if (dispute.Status != (int)DisputeStatus.DecisionPending &&
            dispute.Status != (int)DisputeStatus.UnderReview)
        {
            throw new BadRequestException("Dispute must be under review or pending decision to resolve.");
        }

        var contract = dispute.Contracts;
        var now = _dateTimeService.UtcNow;

        // Validate financial amounts based on resolution type
        ValidateFinancials(command, contract);

        // Execute financial transactions
        if (command.Resolution == DisputeResolution.ClientFavored ||
            command.Resolution == DisputeResolution.Split)
        {
            if (command.RefundToClientAmount.HasValue && command.RefundToClientAmount.Value > 0)
            {
                await ExecuteRefundAsync(contract, dispute, command.RefundToClientAmount.Value, now, cancellationToken);
            }
        }

        if (command.Resolution == DisputeResolution.FreelancerFavored ||
            command.Resolution == DisputeResolution.Split)
        {
            if (command.ReleaseToFreelancerAmount.HasValue && command.ReleaseToFreelancerAmount.Value > 0)
            {
                await ExecuteReleaseAsync(contract, dispute, command.ReleaseToFreelancerAmount.Value, now, cancellationToken);
            }
        }

        // Execute milestone actions
        if (command.MilestoneActions is { Count: > 0 })
        {
            await ExecuteMilestoneActionsAsync(command.MilestoneActions, contract, now, cancellationToken);
        }

        // Execute contract action
        ExecuteContractAction(command, contract, now);

        // Update dispute
        dispute.Status = (int)DisputeStatus.Resolved;
        dispute.Resolution = (int)command.Resolution;
        dispute.ResolutionNote = command.ResolutionNote.Trim();
        dispute.ResolvedByAdminId = command.AdminId;
        dispute.ResolvedAt = now;
        dispute.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);

        // System messages
        await SendResolutionSystemMessagesAsync(dispute, contract, command, now, cancellationToken);

        // Notify participants
        var resolutionLabel = AdminDisputeSupport.GetResolutionLabel(dispute.Resolution) ?? "resolved";
        await AdminDisputeSupport.NotifyParticipantsAsync(
            _notifications, _logger, contract, dispute,
            $"The dispute on contract '{contract.Title}' has been resolved: {resolutionLabel}.",
            cancellationToken);

        return await AdminDisputeSupport.GetDetailAsync(_context, dispute.DisputesId, cancellationToken);
    }

    private static void ValidateFinancials(ResolveAdminDisputeCommand command, Contract contract)
    {
        switch (command.Resolution)
        {
            case DisputeResolution.ClientFavored:
                if (command.ReleaseToFreelancerAmount.HasValue && command.ReleaseToFreelancerAmount.Value > 0)
                    throw new BadRequestException("Client Favored resolution cannot release funds to the freelancer.");
                break;

            case DisputeResolution.FreelancerFavored:
                if (command.RefundToClientAmount.HasValue && command.RefundToClientAmount.Value > 0)
                    throw new BadRequestException("Freelancer Favored resolution cannot refund funds to the client.");
                break;

            case DisputeResolution.Split:
                var refund = command.RefundToClientAmount ?? 0;
                var release = command.ReleaseToFreelancerAmount ?? 0;
                if (refund <= 0 || release <= 0)
                    throw new BadRequestException("Split resolution requires both refund and release amounts.");
                // Note: Total validation requires knowing the escrow balance, done in caller
                break;

            case DisputeResolution.Dismissed:
                if ((command.RefundToClientAmount ?? 0) > 0 || (command.ReleaseToFreelancerAmount ?? 0) > 0)
                    throw new BadRequestException("Dismissed resolution cannot transfer funds.");
                break;
        }
    }

    private async Task ExecuteRefundAsync(
        Contract contract, Dispute dispute, decimal amount, DateTime now,
        CancellationToken cancellationToken)
    {
        var clientUserId = contract.ClientProfiles.UserId;
        var clientWallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(w => w.UserId == clientUserId, cancellationToken)
            ?? throw new BadRequestException("Client wallet not found.");

        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(e => e.ContractsId == contract.ContractsId, cancellationToken);

        var walletTx = new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = clientWallet.UserWalletsId,
            UserId = clientUserId,
            ContractsId = contract.ContractsId,
            ContractEscrowId = escrow?.ContractEscrowId,
            TokenAmount = amount,
            VndAmount = 0,
            Type = (int)WalletTransactionType.EscrowRefund,
            Status = (int)WalletTransactionStatus.Succeeded,
            CreatedAt = now,
            CompletedAt = now
        };
        _context.Set<WalletTransaction>().Add(walletTx);
    }

    private async Task ExecuteReleaseAsync(
        Contract contract, Dispute dispute, decimal amount, DateTime now,
        CancellationToken cancellationToken)
    {
        if (!contract.FreelancerProfilesId.HasValue)
            throw new BadRequestException("Contract does not have a freelancer.");

        var freelancerUserId = await _context.Set<FreelancerProfile>()
            .Where(p => p.FreelancerProfilesId == contract.FreelancerProfilesId.Value)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        var freelancerWallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(w => w.UserId == freelancerUserId, cancellationToken)
            ?? throw new BadRequestException("Freelancer wallet not found.");

        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(e => e.ContractsId == contract.ContractsId, cancellationToken);

        var walletTx = new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = freelancerWallet.UserWalletsId,
            UserId = freelancerUserId,
            ContractsId = contract.ContractsId,
            ContractEscrowId = escrow?.ContractEscrowId,
            TokenAmount = amount,
            VndAmount = 0,
            Type = (int)WalletTransactionType.EscrowRelease,
            Status = (int)WalletTransactionStatus.Succeeded,
            CreatedAt = now,
            CompletedAt = now
        };
        _context.Set<WalletTransaction>().Add(walletTx);
    }

    private async Task ExecuteMilestoneActionsAsync(
        IReadOnlyList<AdminMilestoneAction> actions, Contract contract, DateTime now,
        CancellationToken cancellationToken)
    {
        var milestoneIds = actions.Select(a => a.MilestoneId).ToHashSet();
        var milestones = await _context.Set<Milestone>()
            .Where(m => milestoneIds.Contains(m.MilestonesId) && m.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        foreach (var action in actions)
        {
            var milestone = milestones.FirstOrDefault(m => m.MilestonesId == action.MilestoneId);
            if (milestone is null) continue;

            switch (action.Action)
            {
                case 0: // Approve
                    milestone.Status = (int)MilestoneStatus.Approved;
                    milestone.ApprovedAt = now;
                    break;
                case 1: // Reject
                    milestone.Status = (int)MilestoneStatus.Pending;
                    break;
                case 2: // Cancel (revert to pending)
                    milestone.Status = (int)MilestoneStatus.Pending;
                    break;
            }
        }
    }

    private static void ExecuteContractAction(
        ResolveAdminDisputeCommand command, Contract contract, DateTime now)
    {
        switch (command.ContractAction)
        {
            case AdminContractAction.Resume:
                contract.Status = (int)ContractStatus.Active;
                break;
            case AdminContractAction.Terminate:
                contract.Status = (int)ContractStatus.Cancelled;
                break;
        }
    }

    private async Task SendResolutionSystemMessagesAsync(
        Dispute dispute, Contract contract, ResolveAdminDisputeCommand command, DateTime now,
        CancellationToken cancellationToken)
    {
        var conversation = await _context.Set<Conversation>()
            .Where(c => c.DisputesId == dispute.DisputesId)
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null) return;

        var resolutionLabel = AdminDisputeSupport.GetResolutionLabel((int)command.Resolution) ?? "Resolved";

        // Resolution message
        var msg = ContractConversationEvents.AddSystemMessage(
            _context, conversation,
            $"Decision: {resolutionLabel}. {command.ResolutionNote}",
            now);

        // Financial messages
        if (command.RefundToClientAmount > 0)
        {
            ContractConversationEvents.AddSystemMessage(
                _context, conversation,
                $"{command.RefundToClientAmount:N2} GigCoin refunded to client.",
                now);
        }

        if (command.ReleaseToFreelancerAmount > 0)
        {
            ContractConversationEvents.AddSystemMessage(
                _context, conversation,
                $"{command.ReleaseToFreelancerAmount:N2} GigCoin released to freelancer.",
                now);
        }

        // Contract action message
        var contractActionText = command.ContractAction == AdminContractAction.Resume
            ? "Contract has been resumed."
            : "Contract has been terminated.";
        ContractConversationEvents.AddSystemMessage(_context, conversation, contractActionText, now);

        if (msg is not null)
        {
            try
            {
                await _chatRealtimeNotifier.SendConversationEventAsync(
                    conversation.ConversationsId,
                    "ReceiveMessage",
                    BuildSystemMessagePayload(msg),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to notify conversation for dispute resolution.");
            }
        }
    }

    private static object BuildSystemMessagePayload(Domain.Entities.Message message)
    {
        return new
        {
            messagesId = message.MessagesId,
            conversationsId = message.ConversationsId,
            senderUserId = (Guid?)null,
            messageType = message.MessageType,
            content = message.Content,
            replyToMessageId = (Guid?)null,
            metadata = (string?)null,
            clientMessageId = (string?)null,
            sentAt = message.SentAt,
            editedAt = (DateTime?)null,
            isDeleted = false,
            attachments = Array.Empty<object>()
        };
    }
}
