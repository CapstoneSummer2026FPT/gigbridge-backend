using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Escrow.Client.Fund.DTOs;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Auditing;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Escrow;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.Notifications;
using Domain.Enums.Proposals;
using Domain.Enums.Wallets;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Escrow.Client.Fund.Commands;

public sealed class FundContractEscrowCommandHandler :
    IRequestHandler<FundContractEscrowCommand, FundContractEscrowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly IUserAuditLogService _userAuditLog;

    public FundContractEscrowCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        IUserAuditLogService userAuditLog)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _userAuditLog = userAuditLog;
    }

    public async Task<FundContractEscrowResponse> Handle(
        FundContractEscrowCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        await ContractParticipantGuard.EnsureClientAsync(_context, contract, command.UserId, cancellationToken);

        if (contract.Status == (int)ContractStatus.Active)
        {
            var fundedEscrow = await GetEscrowAsync(contract.ContractsId, cancellationToken);
            return new FundContractEscrowResponse(
                contract.ContractsId,
                fundedEscrow.ContractEscrowId,
                fundedEscrow.RequiredAmount,
                fundedEscrow.RequiredAmount,
                contract.Status,
                fundedEscrow.Status);
        }

        var now = _dateTimeService.UtcNow;
        ContractEscrow escrow;

        if (contract.Status == (int)ContractStatus.PendingSignature)
        {
            await ContractEsignSignatureBridge.EnsureFullySignedContractDocumentAsync(
                _context,
                contract,
                now,
                cancellationToken);

            escrow = await ContractEscrowReadiness.EnsurePendingEscrowAsync(
                _context,
                contract,
                now,
                cancellationToken);
        }
        else if (contract.Status == (int)ContractStatus.PendingEscrow)
        {
            await ContractEsignSignatureBridge.EnsureFullySignedContractDocumentAsync(
                _context,
                contract,
                now,
                cancellationToken);

            escrow = await GetEscrowAsync(contract.ContractsId, cancellationToken);
        }
        else
        {
            throw new BadRequestException("Contract escrow can only be funded after both parties sign.");
        }

        if (escrow.Status == (int)ContractEscrowStatus.Funded)
        {
            contract.Status = (int)ContractStatus.Active;
            await StartFirstMilestoneAsync(contract.ContractsId, now, cancellationToken);
            await FinalizeProposalOutcomeAsync(contract.JobPostsId, contract.ProposalsId, now, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return new FundContractEscrowResponse(
                contract.ContractsId,
                escrow.ContractEscrowId,
                escrow.RequiredAmount,
                escrow.RequiredAmount,
                contract.Status,
                escrow.Status);
        }

        if (escrow.RequiredAmount != contract.TotalBudget)
        {
            throw new BadRequestException("Required escrow funding amount must match contract total budget.");
        }

        if (escrow.RequiredPercentage != 1.0m)
        {
            throw new BadRequestException("Required escrow percentage must be exactly 100%.");
        }

        if (escrow.FundedAmount > 0)
        {
            throw new BadRequestException("Escrow is already partially funded.");
        }

        var wallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(wallet => wallet.UserId == command.UserId, cancellationToken);

        if (wallet is null)
        {
            throw new BadRequestException("Wallet balance is insufficient to fund escrow.");
        }

        var requiredAmount = escrow.RequiredAmount;
        var fundingQuote = ContractEscrowFundingQuote.Calculate(requiredAmount);
        var requiredTokens = fundingQuote.RequiredTokens;
        if (!WalletSpendingService.CanSpend(
            wallet.AvailableTokens,
            wallet.WithdrawableTokens,
            fundingQuote.TotalDebitTokens))
        {
            throw new BadRequestException("Wallet balance is insufficient to fund escrow and pay the service fee.");
        }

        var walletBeforeFunding = WalletBalanceAudit.Snapshot(wallet);
        var fundingUsage = WalletWorkflow.DebitAvailable(
            wallet,
            requiredTokens,
            now,
            "Wallet balance is insufficient to fund escrow.");
        wallet.HeldTokens += requiredTokens;
        var walletAfterHold = WalletBalanceAudit.Snapshot(wallet);

        // Record which GigCoin pools funded the escrow so refunds can restore each pool.
        escrow.DepositedTokens = fundingUsage.DepositedAmount;
        escrow.EarnedTokens = fundingUsage.EarnedAmount;

        await ServiceFeeWorkflow.ChargeAsync(
            _context,
            command.UserId,
            contract.ContractsId,
            requiredAmount,
            $"{ServiceFeeWorkflow.ClientFundingFeePrefix}{contract.ContractsId:N}",
            $"1% client service fee for funding contract: {contract.Title}.",
            now,
            cancellationToken);

        escrow.FundedAmount = escrow.RequiredAmount;
        escrow.Status = (int)ContractEscrowStatus.Funded;
        escrow.FundedAt = now;

        contract.Status = (int)ContractStatus.Active;
        contract.UpdatedAt = now;
        await StartFirstMilestoneAsync(contract.ContractsId, now, cancellationToken);
        await FinalizeProposalOutcomeAsync(contract.JobPostsId, contract.ProposalsId, now, cancellationToken);

        var holdBalanceSource = WalletBalanceAudit.ResolveSource(
            fundingUsage.DepositedAmount,
            fundingUsage.EarnedAmount);
        var (holdDepositedAmount, holdEarnedAmount) = WalletBalanceAudit.ToSplitAmounts(
            fundingUsage.DepositedAmount,
            fundingUsage.EarnedAmount);
        _context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = wallet.UserWalletsId,
            UserId = command.UserId,
            ContractsId = contract.ContractsId,
            ContractEscrowId = escrow.ContractEscrowId,
            TokenAmount = requiredTokens,
            VndAmount = requiredAmount,
            BalanceSource = (int)holdBalanceSource,
            DepositedAmount = holdDepositedAmount,
            EarnedAmount = holdEarnedAmount,
            Type = (int)WalletTransactionType.EscrowHold,
            Status = (int)WalletTransactionStatus.Succeeded,
            GatewayProvider = "InternalTokenWallet",
            GatewayTransactionCode = $"ESCROW-HOLD-{escrow.ContractEscrowId:N}",
            Metadata = WalletBalanceAudit.EnrichMetadata(
                null,
                fundingUsage.DepositedAmount,
                fundingUsage.EarnedAmount,
                walletBeforeFunding,
                walletAfterHold),
            CreatedAt = now,
            CompletedAt = now
        });

        _context.Set<EscrowTransaction>().Add(new EscrowTransaction
        {
            EscrowTransactionId = Guid.NewGuid(),
            ContractEscrowId = escrow.ContractEscrowId,
            Amount = requiredAmount,
            Type = (int)EscrowTransactionType.Deposit,
            Status = (int)EscrowTransactionStatus.Succeeded,
            PaymentGateway = "InternalTokenWallet",
            GatewayTransactionCode = $"ESCROW-HOLD-{escrow.ContractEscrowId:N}",
            Note = "Funded from client token wallet.",
            CreatedAt = now,
            CompletedAt = now
        });

        // Transition conversation type from JobNegotiation to ContractWorkroom
        var conversations = await _context.Set<Conversation>()
            .Where(c => c.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        foreach (var conversation in conversations)
        {
            if (conversation.ConversationType == (int)ConversationType.JobNegotiation)
            {
                conversation.ConversationType = (int)ConversationType.ContractWorkroom;
                conversation.UpdatedAt = now;
            }
        }

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            "Escrow funded. Contract is now active.",
            now,
            cancellationToken);

        _userAuditLog.Add(
            command.UserId,
            UserRole.Client,
            AuditUserActionType.EscrowFunded,
            contract.ContractsId,
            $"Funded contract escrow: {requiredAmount:N2} GigCoin.",
            relatedEntityId: escrow.ContractEscrowId,
            relatedEntityType: nameof(ContractEscrow));

        // Fetch UserIds to create persistent notifications
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(cp => cp.ClientProfilesId == contract.ClientProfilesId, cancellationToken);
        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(fp => fp.FreelancerProfilesId == contract.FreelancerProfilesId, cancellationToken);

        if (clientProfile == null)
        {
            throw new BadRequestException("Client profile not found.");
        }
        if (freelancerProfile == null)
        {
            throw new BadRequestException("Freelancer profile not found.");
        }

        var clientUserId = clientProfile.UserId;
        var freelancerUserId = freelancerProfile.UserId;

        await _notificationService.CreateNotificationAsync(
            clientUserId,
            NotificationType.ContractStarted,
            "Contract Started",
            $"Contract '{contract.Title}' is now active and the workspace is open.",
            contract.ContractsId,
            "Contract",
            cancellationToken);

        await _notificationService.CreateNotificationAsync(
            freelancerUserId,
            NotificationType.ContractStarted,
            "Contract Started",
            $"Contract '{contract.Title}' is now active and the workspace is open.",
            contract.ContractsId,
            "Contract",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var activeConversation = conversations.FirstOrDefault(c => c.ConversationType == (int)ConversationType.ContractWorkroom);
        if (activeConversation != null)
        {
            await _chatRealtimeNotifier.SendConversationEventAsync(
                activeConversation.ConversationsId,
                "WorkspaceOpened",
                new { contractId = contract.ContractsId, conversationId = activeConversation.ConversationsId },
                cancellationToken);
        }

        return new FundContractEscrowResponse(
            contract.ContractsId,
            escrow.ContractEscrowId,
            escrow.RequiredAmount,
            requiredTokens,
            contract.Status,
            escrow.Status);
    }

    private async Task<ContractEscrow> GetEscrowAsync(Guid contractId, CancellationToken cancellationToken)
    {
        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(escrow => escrow.ContractsId == contractId, cancellationToken);

        return escrow ?? throw new NotFoundException("Contract escrow does not exist.");
    }

    private async Task StartFirstMilestoneAsync(Guid contractId, DateTime now, CancellationToken cancellationToken)
    {
        var first = await _context.Set<Milestone>()
            .Where(item => item.ContractsId == contractId && item.Status == (int)MilestoneStatus.Pending)
            .OrderBy(item => item.SortOrder ?? int.MaxValue)
            .ThenBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (first is not null)
        {
            first.Status = (int)MilestoneStatus.InProgress;
            first.StartedAt = now;
            first.UpdatedAt = now;
        }
    }

    /// <summary>
    /// Marks the negotiation as won: the negotiated proposal (if any) becomes Accepted, and
    /// every other Pending/Shortlisted proposal for the same job post is rejected. This is the
    /// single point Proposal.Status reaches Accepted — earlier stages (negotiation, final-offer
    /// acceptance, signing) leave it at Shortlisted so a cancelled contract needs no restoration.
    /// </summary>
    private async Task FinalizeProposalOutcomeAsync(
        Guid jobPostId,
        Guid? acceptedProposalId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (acceptedProposalId.HasValue)
        {
            var acceptedProposal = await _context.Set<Proposal>()
                .FirstOrDefaultAsync(proposal => proposal.ProposalsId == acceptedProposalId.Value, cancellationToken);
            if (acceptedProposal is not null)
            {
                acceptedProposal.Status = (int)ProposalStatus.Accepted;
                acceptedProposal.UpdatedAt = now;
            }
        }

        var siblingProposals = await _context.Set<Proposal>()
            .Where(proposal =>
                proposal.JobPostsId == jobPostId &&
                proposal.ProposalsId != acceptedProposalId &&
                (proposal.Status == (int)ProposalStatus.Pending || proposal.Status == (int)ProposalStatus.Shortlisted))
            .ToListAsync(cancellationToken);

        foreach (var proposal in siblingProposals)
        {
            proposal.Status = (int)ProposalStatus.Rejected;
            proposal.UpdatedAt = now;
        }
    }
}
