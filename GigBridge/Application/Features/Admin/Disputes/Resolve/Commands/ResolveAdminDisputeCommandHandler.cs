using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Admin.Disputes.Common.Internal;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Notifications.Common.DTOs;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Admin.Disputes.Resolve.Commands;

public sealed class ResolveAdminDisputeCommandHandler :
    IRequestHandler<ResolveAdminDisputeCommand, AdminDisputeDetailResponse>
{
    private const decimal Tolerance = 0.01m;
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly IChatRealtimeNotifier _realtime;
    private readonly INotificationSender _notificationSender;
    private readonly ILogger<ResolveAdminDisputeCommandHandler> _logger;

    public ResolveAdminDisputeCommandHandler(
        IApplicationDbContext context,
        IDateTimeService clock,
        IChatRealtimeNotifier realtime,
        INotificationSender notificationSender,
        ILogger<ResolveAdminDisputeCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _realtime = realtime;
        _notificationSender = notificationSender;
        _logger = logger;
    }

    public async Task<AdminDisputeDetailResponse> Handle(
        ResolveAdminDisputeCommand command,
        CancellationToken cancellationToken)
    {
        await AdminDisputeSupport.EnsureAdminAsync(_context, command.AdminId, cancellationToken);
        if (!Enum.IsDefined(command.Resolution))
            throw new BadRequestException("Invalid dispute resolution.");
        if (!Enum.IsDefined(command.ContractAction))
            throw new BadRequestException("Invalid contract action.");
        if (string.IsNullOrWhiteSpace(command.ResolutionNote))
            throw new BadRequestException("Resolution note is required.");

        var dispute = await _context.Set<Dispute>()
            .Include(item => item.Contracts).ThenInclude(item => item.ClientProfiles)
            .Include(item => item.Contracts).ThenInclude(item => item.FreelancerProfiles)
            .Include(item => item.Contracts).ThenInclude(item => item.Milestones)
            .FirstOrDefaultAsync(item => item.DisputesId == command.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        if (dispute.Status is not ((int)DisputeStatus.UnderReview) and
            not ((int)DisputeStatus.WaitingEvidence) and
            not ((int)DisputeStatus.DecisionPending))
        {
            throw new BadRequestException("Dispute must be under review, waiting for evidence, or pending decision.");
        }
        if (dispute.AssignedAdminId != command.AdminId)
            throw new ForbiddenAccessException("Only the assigned administrator may resolve this dispute.");

        var contract = dispute.Contracts;
        if (!contract.FreelancerProfilesId.HasValue || contract.FreelancerProfiles is null)
            throw new BadRequestException("Contract does not have a freelancer.");
        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(item => item.ContractsId == contract.ContractsId, cancellationToken)
            ?? throw new NotFoundException("Contract escrow does not exist.");
        var milestones = contract.Milestones.OrderBy(item => item.SortOrder).ThenBy(item => item.CreatedAt).ToList();
        var inputs = command.MilestoneDecisions
            .GroupBy(item => item.MilestoneId)
            .ToDictionary(group => group.Key, group =>
            {
                if (group.Count() != 1)
                    throw new BadRequestException("Each milestone may have only one administrative decision.");
                return group.Single();
            });

        var required = command.ContractAction == AdminContractAction.Terminate
            ? milestones.Where(item => item.Amount - item.ReleasedAmount > Tolerance).Select(item => item.MilestonesId).ToHashSet()
            : milestones.Where(item => item.Status == (int)MilestoneStatus.Disputed ||
                                       item.MilestonesId == dispute.MilestonesId)
                .Select(item => item.MilestonesId)
                .ToHashSet();
        if (required.Any(id => !inputs.ContainsKey(id)))
            throw new BadRequestException("A decision is required for every affected milestone.");
        if (inputs.Keys.Any(id => milestones.All(item => item.MilestonesId != id)))
            throw new BadRequestException("A milestone decision does not belong to this contract.");

        foreach (var input in inputs.Values)
        {
            if (!Enum.IsDefined(input.Outcome))
                throw new BadRequestException("Invalid milestone outcome.");
            var milestone = milestones.Single(item => item.MilestonesId == input.MilestoneId);
            var allocatable = Math.Max(0m, milestone.Amount - milestone.ReleasedAmount);
            if (input.AdditionalReleaseToFreelancer < 0 || input.RefundToClient < 0 ||
                Math.Abs(input.AdditionalReleaseToFreelancer + input.RefundToClient - allocatable) > Tolerance)
            {
                throw new BadRequestException($"Allocation for milestone '{milestone.Title}' must equal its unreleased escrow amount.");
            }
            if (input.Outcome == DisputeMilestoneOutcome.Accepted && input.RefundToClient > Tolerance)
                throw new BadRequestException("Accepted milestones must release all remaining funds to the freelancer.");
            if (input.Outcome is DisputeMilestoneOutcome.Rejected or DisputeMilestoneOutcome.Cancelled &&
                input.AdditionalReleaseToFreelancer > Tolerance)
                throw new BadRequestException("Rejected or cancelled milestones must refund all remaining funds to the client.");
            if (input.Outcome == DisputeMilestoneOutcome.PartiallyAccepted &&
                (input.AdditionalReleaseToFreelancer <= 0 || input.RefundToClient <= 0))
                throw new BadRequestException("Partially accepted milestones require both a release and a refund.");
        }

        var totalRelease = inputs.Values.Sum(item => item.AdditionalReleaseToFreelancer);
        var totalRefund = inputs.Values.Sum(item => item.RefundToClient);
        var currentRemaining = Math.Max(0m, escrow.FundedAmount - escrow.ReleasedAmount);
        if (totalRelease + totalRefund - currentRemaining > Tolerance)
            throw new BadRequestException("Settlement exceeds the remaining contract escrow.");

        var clientWallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(item => item.UserId == contract.ClientProfiles.UserId, cancellationToken)
            ?? throw new BadRequestException("Client escrow wallet does not exist.");
        if (clientWallet.HeldTokens + Tolerance < totalRelease + totalRefund)
            throw new BadRequestException("Client held wallet balance is insufficient for this settlement.");
        var freelancerWallet = await WalletWorkflow.GetOrCreateWalletAsync(
            _context,
            contract.FreelancerProfiles.UserId,
            _clock.UtcNow,
            cancellationToken);

        var now = _clock.UtcNow;
        var systemMessages = new List<Message>();
        var notifications = new List<Notification>();
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

        clientWallet.HeldTokens -= totalRelease + totalRefund;
        clientWallet.AvailableTokens += totalRefund;
        clientWallet.UpdatedAt = now;
        if (totalRelease > 0)
            WalletWorkflow.CreditWithdrawable(freelancerWallet, totalRelease, now);

        foreach (var input in inputs.Values)
        {
            var milestone = milestones.Single(item => item.MilestonesId == input.MilestoneId);
            _context.Set<DisputeMilestoneDecision>().Add(new DisputeMilestoneDecision
            {
                DisputeMilestoneDecisionId = Guid.NewGuid(),
                DisputesId = dispute.DisputesId,
                MilestonesId = milestone.MilestonesId,
                Outcome = (int)input.Outcome,
                MilestoneAmountSnapshot = milestone.Amount,
                ReleasedAmountSnapshot = milestone.ReleasedAmount,
                AdditionalReleaseAmount = input.AdditionalReleaseToFreelancer,
                RefundAmount = input.RefundToClient,
                DecidedByAdminId = command.AdminId,
                CreatedAt = now
            });

            if (input.AdditionalReleaseToFreelancer > 0)
                AddReleaseLedger(contract, escrow, milestone, clientWallet, freelancerWallet, dispute, input.AdditionalReleaseToFreelancer, now);
            if (input.RefundToClient > 0)
                AddRefundLedger(contract, escrow, milestone, clientWallet, dispute, input.RefundToClient, now);

            milestone.ReleasedAmount += input.AdditionalReleaseToFreelancer;
            milestone.LastReleasedAt = input.AdditionalReleaseToFreelancer > 0 ? now : milestone.LastReleasedAt;
            milestone.Status = input.Outcome switch
            {
                DisputeMilestoneOutcome.Accepted or DisputeMilestoneOutcome.PartiallyAccepted => (int)MilestoneStatus.Approved,
                DisputeMilestoneOutcome.Rejected => (int)MilestoneStatus.InProgress,
                _ => (int)MilestoneStatus.Cancelled
            };
            milestone.ApprovedAt = milestone.Status == (int)MilestoneStatus.Approved ? now : milestone.ApprovedAt;
            milestone.UpdatedAt = now;
            AddAudit(command.AdminId, dispute.DisputesId, "Dispute.MilestoneDecision", new
            {
                milestone.MilestonesId,
                outcome = input.Outcome.ToString(),
                input.AdditionalReleaseToFreelancer,
                input.RefundToClient
            }, now);
        }

        escrow.ReleasedAmount += totalRelease;
        escrow.FundedAmount -= totalRefund;
        var remaining = Math.Max(0m, escrow.FundedAmount - escrow.ReleasedAmount);
        if (command.ContractAction == AdminContractAction.Terminate && remaining > Tolerance)
            throw new BadRequestException("Terminating a contract requires allocating all remaining escrow.");
        escrow.Status = remaining <= Tolerance
            ? totalRelease > 0 ? (int)ContractEscrowStatus.Released : (int)ContractEscrowStatus.Refunded
            : escrow.ReleasedAmount > 0 ? (int)ContractEscrowStatus.PartiallyReleased : (int)ContractEscrowStatus.Funded;
        escrow.ReleasedAt = escrow.Status == (int)ContractEscrowStatus.Released ? now : escrow.ReleasedAt;
        escrow.RefundedAt = escrow.Status == (int)ContractEscrowStatus.Refunded ? now : escrow.RefundedAt;

        contract.Status = command.ContractAction == AdminContractAction.Resume
            ? (int)ContractStatus.Active
            : (int)ContractStatus.Cancelled;
        contract.UpdatedAt = now;
        dispute.Status = (int)DisputeStatus.Resolved;
        dispute.Resolution = (int)command.Resolution;
        dispute.ResolutionNote = command.ResolutionNote.Trim();
        dispute.ResolvedByAdminId = command.AdminId;
        dispute.ResolvedAt = now;
        dispute.UpdatedAt = now;

        AddAudit(command.AdminId, dispute.DisputesId, totalRefund > 0 ? "Dispute.EscrowRefund" : "Dispute.NoRefund", new { totalRefund }, now);
        AddAudit(command.AdminId, dispute.DisputesId, totalRelease > 0 ? "Dispute.EscrowRelease" : "Dispute.NoRelease", new { totalRelease }, now);
        AddAudit(command.AdminId, dispute.DisputesId,
            command.ContractAction == AdminContractAction.Resume ? "Dispute.ContractResume" : "Dispute.ContractTermination",
            new { contract.ContractsId, remaining }, now);
        AddAudit(command.AdminId, dispute.DisputesId, "Dispute.FinalResolution", new
        {
            resolution = command.Resolution.ToString(),
            command.ResolutionNote,
            command.InternalNotes,
            totalRelease,
            totalRefund,
            remaining
        }, now);

        var conversation = await _context.Set<Conversation>()
            .FirstOrDefaultAsync(item => item.DisputesId == dispute.DisputesId, cancellationToken);
        if (conversation is not null)
        {
            AddSystemMessage(conversation, $"Milestone decisions recorded for {inputs.Count} milestone(s).", now, systemMessages);
            if (totalRefund > 0) AddSystemMessage(conversation, $"{totalRefund:N2} GigCoin refunded to the client.", now, systemMessages);
            if (totalRelease > 0) AddSystemMessage(conversation, $"{totalRelease:N2} GigCoin released to the freelancer.", now, systemMessages);
            AddSystemMessage(conversation,
                command.ContractAction == AdminContractAction.Resume ? "Contract has been resumed." : "Contract has been terminated.",
                now,
                systemMessages);
            AddSystemMessage(conversation,
                $"Final decision: {AdminDisputeSupport.GetResolutionLabel((int)command.Resolution)}. {command.ResolutionNote.Trim()}",
                now,
                systemMessages);
        }

        foreach (var userId in new[] { contract.ClientProfiles.UserId, contract.FreelancerProfiles.UserId }.Distinct())
        {
            var notification = new Notification
            {
                NotificationsId = Guid.NewGuid(),
                UserId = userId,
                Type = (int)NotificationType.DisputeUpdate,
                Title = "Dispute resolved",
                Content = $"The dispute for '{contract.Title}' was resolved. Released: {totalRelease:N2}; refunded: {totalRefund:N2} GigCoin.",
                ReferenceId = contract.ContractsId,
                ReferenceType = nameof(Contract),
                IsRead = false,
                CreatedAt = now
            };
            notifications.Add(notification);
            _context.Set<Notification>().Add(notification);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var message in systemMessages)
        {
            try
            {
                await _realtime.SendConversationEventAsync(message.ConversationsId, "ReceiveMessage", ToMessagePayload(message), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Failed to publish dispute resolution message {MessageId}.", message.MessagesId);
            }
        }
        foreach (var notification in notifications)
        {
            try
            {
                await _notificationSender.SendToUserAsync(notification.UserId, ToNotificationDto(notification), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Failed to deliver dispute resolution notification.");
            }
        }

        return await AdminDisputeSupport.GetDetailAsync(_context, dispute.DisputesId, cancellationToken);
    }

    private void AddReleaseLedger(Contract contract, ContractEscrow escrow, Milestone milestone,
        UserWallet clientWallet, UserWallet freelancerWallet, Dispute dispute, decimal amount, DateTime now)
    {
        var code = $"DISPUTE-RELEASE-{dispute.DisputesId:N}-{milestone.MilestonesId:N}";
        foreach (var wallet in new[] { clientWallet, freelancerWallet })
        {
            _context.Set<WalletTransaction>().Add(new WalletTransaction
            {
                WalletTransactionsId = Guid.NewGuid(), UserWalletsId = wallet.UserWalletsId, UserId = wallet.UserId,
                ContractsId = contract.ContractsId, ContractEscrowId = escrow.ContractEscrowId,
                MilestonesId = milestone.MilestonesId, TokenAmount = amount, VndAmount = amount,
                Type = (int)WalletTransactionType.EscrowRelease, Status = (int)WalletTransactionStatus.Succeeded,
                IdempotencyKey = code, GatewayProvider = "AdminDisputeResolution", GatewayTransactionCode = code,
                Note = "Released through dispute resolution.", CreatedAt = now, CompletedAt = now
            });
        }
        _context.Set<EscrowTransaction>().Add(new EscrowTransaction
        {
            EscrowTransactionId = Guid.NewGuid(), ContractEscrowId = escrow.ContractEscrowId,
            MilestonesId = milestone.MilestonesId, Amount = amount,
            Type = (int)EscrowTransactionType.ReleaseToFreelancer, Status = (int)EscrowTransactionStatus.Succeeded,
            PaymentGateway = "AdminDisputeResolution", GatewayTransactionCode = code,
            Note = "Released through dispute resolution.", CreatedAt = now, CompletedAt = now
        });
    }

    private void AddRefundLedger(Contract contract, ContractEscrow escrow, Milestone milestone,
        UserWallet clientWallet, Dispute dispute, decimal amount, DateTime now)
    {
        var code = $"DISPUTE-REFUND-{dispute.DisputesId:N}-{milestone.MilestonesId:N}";
        _context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(), UserWalletsId = clientWallet.UserWalletsId,
            UserId = clientWallet.UserId, ContractsId = contract.ContractsId,
            ContractEscrowId = escrow.ContractEscrowId, MilestonesId = milestone.MilestonesId,
            TokenAmount = amount, VndAmount = amount, Type = (int)WalletTransactionType.EscrowRefund,
            Status = (int)WalletTransactionStatus.Succeeded, IdempotencyKey = code,
            GatewayProvider = "AdminDisputeResolution", GatewayTransactionCode = code,
            Note = "Refunded through dispute resolution.", CreatedAt = now, CompletedAt = now
        });
        _context.Set<EscrowTransaction>().Add(new EscrowTransaction
        {
            EscrowTransactionId = Guid.NewGuid(), ContractEscrowId = escrow.ContractEscrowId,
            MilestonesId = milestone.MilestonesId, Amount = amount,
            Type = (int)EscrowTransactionType.RefundToClient, Status = (int)EscrowTransactionStatus.Succeeded,
            PaymentGateway = "AdminDisputeResolution", GatewayTransactionCode = code,
            Note = "Refunded through dispute resolution.", CreatedAt = now, CompletedAt = now
        });
    }

    private void AddAudit(Guid adminId, Guid disputeId, string action, object values, DateTime now) =>
        _context.Set<AdminAuditLog>().Add(new AdminAuditLog
        {
            AdminAuditLogsId = Guid.NewGuid(), AdminId = adminId, Action = action,
            EntityId = disputeId, EntityType = nameof(Dispute),
            NewValues = JsonSerializer.Serialize(values), CreatedAt = now
        });

    private void AddSystemMessage(Conversation conversation, string content, DateTime now, ICollection<Message> messages)
    {
        var message = ContractConversationEvents.AddSystemMessage(_context, conversation, content, now);
        if (message is not null) messages.Add(message);
    }

    private static object ToMessagePayload(Message message) => new
    {
        messagesId = message.MessagesId, conversationsId = message.ConversationsId,
        senderUserId = (Guid?)null, messageType = message.MessageType, content = message.Content,
        sentAt = message.SentAt, attachments = Array.Empty<object>()
    };

    private static NotificationDto ToNotificationDto(Notification notification) => new()
    {
        Id = notification.NotificationsId, Source = "Personal", NotificationId = notification.NotificationsId,
        ReadTargetId = notification.NotificationsId, Type = (NotificationType)notification.Type,
        Title = notification.Title, Content = notification.Content, ReferenceId = notification.ReferenceId,
        ReferenceType = notification.ReferenceType, IsRead = notification.IsRead ?? false, CreatedAt = notification.CreatedAt
    };
}
