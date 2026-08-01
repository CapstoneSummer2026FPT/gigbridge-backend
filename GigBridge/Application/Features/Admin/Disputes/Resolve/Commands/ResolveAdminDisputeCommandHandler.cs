using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Admin.Disputes.Common.Internal;
using Application.Features.Contracts.Common.Internal;
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
    private readonly ILogger<ResolveAdminDisputeCommandHandler> _logger;

    public ResolveAdminDisputeCommandHandler(
        IApplicationDbContext context,
        IDateTimeService clock,
        IChatRealtimeNotifier realtime,
        ILogger<ResolveAdminDisputeCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task<AdminDisputeDetailResponse> Handle(
        ResolveAdminDisputeCommand command,
        CancellationToken cancellationToken)
    {
        await AdminDisputeSupport.EnsureAdminAsync(_context, command.AdminId, cancellationToken);
        ValidateHeader(command);

        var contractId = await _context.Set<Dispute>()
            .AsNoTracking()
            .Where(item => item.DisputesId == command.DisputeId)
            .Select(item => (Guid?)item.ContractsId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        var systemMessages = new List<Message>();
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            ContractEscrowLock.ForContract(contractId), cancellationToken);

        var dispute = await _context.Set<Dispute>()
            .Include(item => item.Contracts).ThenInclude(item => item.ClientProfiles).ThenInclude(item => item.User)
            .Include(item => item.Contracts).ThenInclude(item => item.FreelancerProfiles).ThenInclude(item => item!.User)
            .Include(item => item.Contracts).ThenInclude(item => item.Milestones)
            .FirstOrDefaultAsync(item => item.DisputesId == command.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        if (dispute.Status is (int)DisputeStatus.Resolved or (int)DisputeStatus.Closed)
            throw new ConflictException("This dispute has already been resolved.");
        if (dispute.Status is not ((int)DisputeStatus.UnderReview) and
            not ((int)DisputeStatus.WaitingEvidence) and not ((int)DisputeStatus.DecisionPending))
            throw new ConflictException("The dispute status changed before resolution.");
        if (dispute.AssignedAdminId != command.AdminId)
            throw new ForbiddenAccessException("Only the assigned administrator may resolve this dispute.");
        if (await _context.Set<DisputeMilestoneDecision>()
            .AnyAsync(item => item.DisputesId == dispute.DisputesId, cancellationToken))
            throw new ConflictException("Milestone decisions already exist for this dispute.");

        var contract = dispute.Contracts;
        if (!contract.FreelancerProfilesId.HasValue || contract.FreelancerProfiles is null)
            throw new BadRequestException("Contract does not have a freelancer.");

        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(item => item.ContractsId == contract.ContractsId, cancellationToken)
            ?? throw new NotFoundException("Contract escrow does not exist.");
        var milestones = contract.Milestones.OrderBy(item => item.SortOrder).ThenBy(item => item.CreatedAt).ToList();
        var ledger = await _context.Set<EscrowTransaction>()
            .Where(item => item.ContractEscrowId == escrow.ContractEscrowId &&
                           item.Status == (int)EscrowTransactionStatus.Succeeded &&
                           item.MilestonesId.HasValue)
            .ToListAsync(cancellationToken);
        var refundByMilestone = ledger.Where(item => item.Type == (int)EscrowTransactionType.RefundToClient)
            .GroupBy(item => item.MilestonesId!.Value).ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));
        var penaltyByMilestone = ledger.Where(item => item.Type == (int)EscrowTransactionType.DisputePenalty)
            .GroupBy(item => item.MilestonesId!.Value).ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));
        decimal Locked(Milestone milestone) => Math.Max(0m, milestone.Amount - milestone.ReleasedAmount -
            refundByMilestone.GetValueOrDefault(milestone.MilestonesId) -
            penaltyByMilestone.GetValueOrDefault(milestone.MilestonesId));

        var allLocked = milestones.Sum(Locked);
        var escrowBefore = Math.Max(0m, escrow.FundedAmount - escrow.ReleasedAmount);
        if (Math.Abs(allLocked - escrowBefore) >= Tolerance)
            throw new ConflictException("Escrow and milestone balances changed or are inconsistent. Refresh and retry.");

        var editable = milestones.Where(item => Locked(item) >= Tolerance &&
            (command.ContractAction == AdminContractAction.Terminate ||
             item.Status == (int)MilestoneStatus.Disputed || item.MilestonesId == dispute.MilestonesId))
            .ToDictionary(item => item.MilestonesId);
        var inputs = command.MilestoneAllocations.GroupBy(item => item.MilestoneId).ToList();
        if (inputs.Any(group => group.Count() != 1))
            throw new BadRequestException("Each milestone may have only one administrative allocation.");
        var allocations = inputs.ToDictionary(group => group.Key, group => group.Single());
        if (editable.Keys.Any(id => !allocations.ContainsKey(id)))
            throw new BadRequestException("An allocation is required for every affected milestone.");
        if (allocations.Keys.Any(id => !editable.ContainsKey(id)))
            throw new BadRequestException("An allocation is out of scope, fully processed, or belongs to another contract.");

        foreach (var allocation in allocations.Values)
            ValidateAllocation(allocation, editable[allocation.MilestoneId], Locked(editable[allocation.MilestoneId]));

        var totalRelease = allocations.Values.Sum(item => item.FreelancerAward);
        var totalRefund = allocations.Values.Sum(item => item.ClientRefund);
        var totalPenalty = allocations.Values.Sum(item => item.PenaltyAmount);
        var amountBeingResolved = editable.Values.Sum(Locked);
        if (Math.Abs(totalRelease + totalRefund + totalPenalty - amountBeingResolved) >= Tolerance)
            throw new BadRequestException("The dispute allocation must equal the escrow amount being resolved.");

        var violatingUserIds = new List<Guid>();
        if (command.ClientViolation.IsViolation) violatingUserIds.Add(contract.ClientProfiles.UserId);
        if (command.FreelancerViolation.IsViolation) violatingUserIds.Add(contract.FreelancerProfiles.UserId);
        foreach (var userId in violatingUserIds.Distinct().OrderBy(id => id))
            await transaction.AcquireTransactionLockAsync(ContractEscrowLock.ForUser(userId), cancellationToken);

        var client = await _context.Set<User>().FirstAsync(item => item.UserId == contract.ClientProfiles.UserId, cancellationToken);
        var freelancer = await _context.Set<User>().FirstAsync(item => item.UserId == contract.FreelancerProfiles.UserId, cancellationToken);
        var clientBefore = AccountSnapshot(client);
        var freelancerBefore = AccountSnapshot(freelancer);
        var disputeStatusBefore = dispute.Status;
        var contractStatusBefore = contract.Status;
        var fundedBefore = escrow.FundedAmount;
        var releasedBefore = escrow.ReleasedAmount;
        var clientWallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(item => item.UserId == client.UserId, cancellationToken)
            ?? throw new BadRequestException("Client escrow wallet does not exist.");
        var freelancerWallet = await WalletWorkflow.GetOrCreateWalletAsync(
            _context, freelancer.UserId, _clock.UtcNow, cancellationToken);
        var now = _clock.UtcNow;
        var clientViolation = await ApplyViolationAsync(client, command.ClientViolation, dispute,
            contract, command.AdminId, now, cancellationToken);
        var freelancerViolation = await ApplyViolationAsync(freelancer, command.FreelancerViolation,
            dispute, contract, command.AdminId, now, cancellationToken);
        var violatingUserId = command.ClientViolation.IsViolation ^ command.FreelancerViolation.IsViolation
            ? command.ClientViolation.IsViolation ? client.UserId : freelancer.UserId
            : (Guid?)null;

        foreach (var allocation in allocations.Values)
        {
            var milestone = editable[allocation.MilestoneId];
            _context.Set<DisputeMilestoneDecision>().Add(new DisputeMilestoneDecision
            {
                DisputeMilestoneDecisionId = Guid.NewGuid(), DisputesId = dispute.DisputesId,
                MilestonesId = milestone.MilestonesId, Outcome = (int)allocation.Outcome,
                MilestoneAmountSnapshot = milestone.Amount, ReleasedAmountSnapshot = milestone.ReleasedAmount,
                AdditionalReleaseAmount = allocation.FreelancerAward, RefundAmount = allocation.ClientRefund,
                PenaltyAmount = allocation.PenaltyAmount, Reason = allocation.Reason?.Trim(),
                DecidedByAdminId = command.AdminId, CreatedAt = now
            });

            if (allocation.FreelancerAward > 0m)
                AddReleaseLedger(contract, escrow, milestone, clientWallet, freelancerWallet,
                    dispute, allocation.FreelancerAward, now);
            if (allocation.ClientRefund > 0m)
                AddRefundLedger(contract, escrow, milestone, clientWallet, dispute, allocation.ClientRefund, now);
            if (allocation.PenaltyAmount > 0m)
                AddPenaltyLedger(contract, escrow, milestone, clientWallet, dispute,
                    allocation, violatingUserId, command.AdminId, command.ResolutionNote, now);

            milestone.ReleasedAmount += allocation.FreelancerAward;
            if (allocation.FreelancerAward > 0m) milestone.LastReleasedAt = now;
            milestone.Status = allocation.Outcome switch
            {
                DisputeMilestoneOutcome.Accepted or DisputeMilestoneOutcome.PartiallyAccepted => (int)MilestoneStatus.Approved,
                DisputeMilestoneOutcome.Rejected => (int)MilestoneStatus.InProgress,
                _ => (int)MilestoneStatus.Cancelled
            };
            if (milestone.Status == (int)MilestoneStatus.Approved) milestone.ApprovedAt ??= now;
            milestone.UpdatedAt = now;
        }

        escrow.ReleasedAmount += totalRelease;
        escrow.FundedAmount -= totalRefund + totalPenalty;
        var escrowAfter = Math.Max(0m, escrow.FundedAmount - escrow.ReleasedAmount);
        if (command.ContractAction == AdminContractAction.Terminate && escrowAfter >= Tolerance)
            throw new ConflictException("Terminating the contract requires resolving all remaining escrow.");
        escrow.Status = escrowAfter < Tolerance
            ? escrow.ReleasedAmount > 0m ? (int)ContractEscrowStatus.Released : (int)ContractEscrowStatus.Refunded
            : escrow.ReleasedAmount > 0m ? (int)ContractEscrowStatus.PartiallyReleased : (int)ContractEscrowStatus.Funded;
        if (escrow.Status == (int)ContractEscrowStatus.Released) escrow.ReleasedAt ??= now;
        if (escrow.Status == (int)ContractEscrowStatus.Refunded) escrow.RefundedAt ??= now;

        contract.Status = command.ContractAction == AdminContractAction.Resume
            ? (int)ContractStatus.Active : (int)ContractStatus.Cancelled;
        contract.UpdatedAt = now;
        dispute.Status = (int)DisputeStatus.Resolved;
        dispute.Resolution = (int)command.Resolution;
        dispute.ResolutionNote = command.ResolutionNote.Trim();
        dispute.ResolvedByAdminId = command.AdminId;
        dispute.ResolvedAt = now;
        dispute.UpdatedAt = now;

        var auditId = Guid.NewGuid();
        _context.Set<AdminAuditLog>().Add(new AdminAuditLog
        {
            AdminAuditLogsId = auditId, AdminId = command.AdminId,
            Action = "Dispute.FinalResolution", EntityId = dispute.DisputesId,
            EntityType = nameof(Dispute), CreatedAt = now,
            OldValues = JsonSerializer.Serialize(new
            {
                disputeStatus = disputeStatusBefore,
                contractStatus = contractStatusBefore,
                escrow = new { remaining = escrowBefore, funded = fundedBefore, released = releasedBefore },
                client = clientBefore, freelancer = freelancerBefore
            }),
            NewValues = JsonSerializer.Serialize(new
            {
                resolution = command.Resolution.ToString(), command.ResolutionNote, command.InternalNotes,
                contractAction = command.ContractAction.ToString(), escrowBefore, escrowAfter,
                totalFreelancerAward = totalRelease, totalClientRefund = totalRefund, totalPenalty,
                allocations = allocations.Values, clientViolation, freelancerViolation,
                client = AccountSnapshot(client), freelancer = AccountSnapshot(freelancer)
            })
        });

        var conversation = await _context.Set<Conversation>()
            .FirstOrDefaultAsync(item => item.DisputesId == dispute.DisputesId, cancellationToken);
        if (conversation is not null)
        {
            AddSystemMessage(conversation, $"Milestone decisions recorded for {allocations.Count} milestone(s).", now, systemMessages);
            if (totalRefund > 0) AddSystemMessage(conversation, $"{totalRefund:N2} VND refunded to the client.", now, systemMessages);
            if (totalRelease > 0) AddSystemMessage(conversation, $"{totalRelease:N2} VND released to the freelancer.", now, systemMessages);
            if (totalPenalty > 0) AddSystemMessage(conversation, $"{totalPenalty:N2} VND retained by the platform as a dispute penalty.", now, systemMessages);
            AddSystemMessage(conversation, command.ContractAction == AdminContractAction.Resume
                ? "Contract has been resumed." : "Contract has been terminated.", now, systemMessages);
            AddSystemMessage(conversation,
                $"Final decision: {AdminDisputeSupport.GetResolutionLabel((int)command.Resolution)}. {command.ResolutionNote.Trim()}",
                now, systemMessages);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConflictException("Escrow or account state changed during resolution. Refresh and retry.", exception);
        }
        catch (DbUpdateException exception)
        {
            throw new ConflictException("This dispute resolution was already processed or conflicted with another financial operation.", exception);
        }

        foreach (var message in systemMessages)
        {
            try
            {
                await _realtime.SendConversationEventAsync(message.ConversationsId, "ReceiveMessage",
                    ToMessagePayload(message), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Failed to publish dispute resolution message {MessageId}.", message.MessagesId);
            }
        }

        return await AdminDisputeSupport.GetDetailAsync(_context, dispute.DisputesId, cancellationToken);
    }

    private static void ValidateHeader(ResolveAdminDisputeCommand command)
    {
        if (!Enum.IsDefined(command.Resolution)) throw new BadRequestException("Invalid dispute resolution.");
        if (!Enum.IsDefined(command.ContractAction)) throw new BadRequestException("Invalid contract action.");
        if (string.IsNullOrWhiteSpace(command.ResolutionNote)) throw new BadRequestException("Resolution note is required.");
        ValidateViolation(command.ClientViolation, "Client");
        ValidateViolation(command.FreelancerViolation, "Freelancer");
    }

    private static void ValidateViolation(AdminViolationInput input, string party)
    {
        if (!input.IsViolation) return;
        if (!input.ViolationType.HasValue || !Enum.IsDefined(input.ViolationType.Value))
            throw new BadRequestException($"{party} violation type is required.");
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw new BadRequestException($"{party} violation reason is required.");
    }

    private static void ValidateAllocation(AdminMilestoneAllocationInput input, Milestone milestone, decimal locked)
    {
        if (!Enum.IsDefined(input.Outcome)) throw new BadRequestException("Invalid milestone outcome.");
        if (input.FreelancerAward < 0m || input.ClientRefund < 0m || input.PenaltyAmount < 0m)
            throw new BadRequestException("Allocation amounts cannot be negative.");
        if (Math.Abs(input.FreelancerAward + input.ClientRefund + input.PenaltyAmount - locked) >= Tolerance)
            throw new BadRequestException($"Allocation for milestone '{milestone.Title}' must equal its locked amount.");
        if (input.PenaltyAmount > 0m && string.IsNullOrWhiteSpace(input.Reason))
            throw new BadRequestException($"A reason is required for the penalty on milestone '{milestone.Title}'.");

        var differsFromSuggestion = milestone.Status switch
        {
            (int)MilestoneStatus.Submitted or (int)MilestoneStatus.Approved =>
                Math.Abs(input.FreelancerAward - locked) >= Tolerance || input.ClientRefund >= Tolerance || input.PenaltyAmount >= Tolerance,
            (int)MilestoneStatus.Pending or (int)MilestoneStatus.InProgress =>
                Math.Abs(input.ClientRefund - locked) >= Tolerance || input.FreelancerAward >= Tolerance || input.PenaltyAmount >= Tolerance,
            _ => false
        };
        if (differsFromSuggestion && string.IsNullOrWhiteSpace(input.Reason))
            throw new BadRequestException($"A reason is required when overriding the suggested allocation for '{milestone.Title}'.");
    }

    private async Task<object?> ApplyViolationAsync(User user, AdminViolationInput input, Dispute dispute,
        Contract contract, Guid adminId, DateTime now, CancellationToken cancellationToken)
    {
        if (!input.IsViolation) return null;
        var existing = await _context.Set<UserViolation>()
            .AnyAsync(item => item.UserId == user.UserId && item.DisputeId == dispute.DisputesId, cancellationToken);
        if (existing) return new { duplicate = true, user.ViolationCount, user.AccountStatus };

        var wasBanned = user.AccountStatus == (int)AccountStatus.Banned || !user.IsActive;
        var wasSuspended = user.AccountStatus == (int)AccountStatus.Suspended &&
            user.SuspendedUntil.HasValue && user.SuspendedUntil.Value > now;
        user.ViolationCount++;
        user.IsFlagged = true;
        user.UpdatedAt = now;
        var action = UserViolationAction.Warning;
        DateTime? suspendedUntil = null;
        if (wasBanned || user.ViolationCount >= 3)
        {
            action = UserViolationAction.PermanentBan;
            user.AccountStatus = (int)AccountStatus.Banned;
            user.IsActive = false;
            user.BannedAt ??= now;
            user.BanReason = input.Reason!.Trim();
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiry = null;
        }
        else if (user.ViolationCount == 2)
        {
            action = UserViolationAction.SevenDaySuspension;
            user.AccountStatus = (int)AccountStatus.Suspended;
            user.IsActive = true;
            user.SuspendedAt = now;
            user.SuspendedUntil = now.AddDays(7);
            user.SuspensionReason = input.Reason!.Trim();
            suspendedUntil = user.SuspendedUntil;
        }
        else
        {
            user.AccountStatus = wasSuspended ? (int)AccountStatus.Suspended : (int)AccountStatus.Active;
            user.IsActive = true;
        }

        _context.Set<UserViolation>().Add(new UserViolation
        {
            UserViolationId = Guid.NewGuid(), UserId = user.UserId, DisputeId = dispute.DisputesId,
            ContractId = contract.ContractsId, MilestoneId = dispute.MilestonesId,
            ViolationNumber = user.ViolationCount, ViolationType = (int)input.ViolationType!.Value,
            Reason = input.Reason!.Trim(), Description = input.Description?.Trim(),
            ActionTaken = (int)action, SuspendedUntil = suspendedUntil,
            CreatedByAdminId = adminId, CreatedAt = now, IsActive = true
        });
        return new { duplicate = false, user.ViolationCount, user.AccountStatus, action, suspendedUntil };
    }

    private void AddReleaseLedger(Contract contract, ContractEscrow escrow, Milestone milestone,
        UserWallet clientWallet, UserWallet freelancerWallet, Dispute dispute, decimal amount, DateTime now)
    {
        var code = $"DISPUTE-RELEASE-{dispute.DisputesId:N}-{milestone.MilestonesId:N}";
        ContractEscrowWalletWorkflow.Release(_context, clientWallet, freelancerWallet, contract.ContractsId,
            escrow.ContractEscrowId, milestone.MilestonesId, amount, code, "AdminDisputeResolution",
            "Released through dispute resolution.", now);
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
        ContractEscrowWalletWorkflow.Refund(_context, clientWallet, contract.ContractsId,
            escrow.ContractEscrowId, milestone.MilestonesId, amount, code, "AdminDisputeResolution",
            "Refunded through dispute resolution.", now);
        _context.Set<EscrowTransaction>().Add(new EscrowTransaction
        {
            EscrowTransactionId = Guid.NewGuid(), ContractEscrowId = escrow.ContractEscrowId,
            MilestonesId = milestone.MilestonesId, Amount = amount,
            Type = (int)EscrowTransactionType.RefundToClient, Status = (int)EscrowTransactionStatus.Succeeded,
            PaymentGateway = "AdminDisputeResolution", GatewayTransactionCode = code,
            Note = "Refunded through dispute resolution.", CreatedAt = now, CompletedAt = now
        });
    }

    private void AddPenaltyLedger(Contract contract, ContractEscrow escrow, Milestone milestone,
        UserWallet clientWallet, Dispute dispute,
        AdminMilestoneAllocationInput allocation, Guid? violatingUserId, Guid adminId,
        string resolutionNote, DateTime now)
    {
        var debitCode = $"DISPUTE-PENALTY-DEBIT-{dispute.DisputesId:N}-{milestone.MilestonesId:N}";
        var escrowCode = $"DISPUTE-PENALTY-{dispute.DisputesId:N}-{milestone.MilestonesId:N}";
        var clientDebit = ContractEscrowWalletWorkflow.Penalty(_context, clientWallet,
            contract.ContractsId, escrow.ContractEscrowId, milestone.MilestonesId, dispute.DisputesId,
            allocation.PenaltyAmount, debitCode, allocation.Reason!.Trim(), now);
        var escrowTransaction = new EscrowTransaction
        {
            EscrowTransactionId = Guid.NewGuid(), ContractEscrowId = escrow.ContractEscrowId,
            MilestonesId = milestone.MilestonesId, Amount = allocation.PenaltyAmount,
            Type = (int)EscrowTransactionType.DisputePenalty, Status = (int)EscrowTransactionStatus.Succeeded,
            PaymentGateway = "AdminDisputeResolution", GatewayTransactionCode = escrowCode,
            Note = allocation.Reason!.Trim(), CreatedAt = now, CompletedAt = now
        };
        _context.Set<EscrowTransaction>().Add(escrowTransaction);
        _context.Set<DisputePenalty>().Add(new DisputePenalty
        {
            DisputePenaltyId = Guid.NewGuid(), DisputeId = dispute.DisputesId,
            ContractId = contract.ContractsId, MilestoneId = milestone.MilestonesId,
            ViolatingUserId = violatingUserId, Amount = allocation.PenaltyAmount,
            Reason = allocation.Reason!.Trim(), ResolutionNote = resolutionNote.Trim(),
            CreatedByAdminId = adminId,
            ClientDebitWalletTransactionId = clientDebit.WalletTransactionsId,
            EscrowTransactionId = escrowTransaction.EscrowTransactionId,
            Status = (int)EscrowTransactionStatus.Succeeded, CreatedAt = now
        });
    }

    private void AddSystemMessage(Conversation conversation, string content, DateTime now,
        ICollection<Message> messages)
    {
        var message = ContractConversationEvents.AddSystemMessage(_context, conversation, content, now);
        if (message is not null) messages.Add(message);
    }

    private static object AccountSnapshot(User user) => new
    {
        user.UserId, user.ViolationCount, user.IsFlagged, user.AccountStatus,
        user.IsActive, user.SuspendedAt, user.SuspendedUntil, user.BannedAt
    };

    private static object ToMessagePayload(Message message) => new
    {
        messagesId = message.MessagesId, conversationsId = message.ConversationsId,
        senderUserId = (Guid?)null, messageType = message.MessageType, content = message.Content,
        sentAt = message.SentAt, attachments = Array.Empty<object>()
    };
}
