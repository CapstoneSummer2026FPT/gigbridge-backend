using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Escrow.Client.Fund.DTOs;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
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

    public FundContractEscrowCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
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
        var requiredTokens = requiredAmount;
        WalletWorkflow.DebitAvailable(wallet, requiredTokens, now, "Wallet balance is insufficient to fund escrow.");
        wallet.HeldTokens += requiredTokens;

        escrow.FundedAmount = escrow.RequiredAmount;
        escrow.Status = (int)ContractEscrowStatus.Funded;
        escrow.FundedAt = now;

        contract.Status = (int)ContractStatus.Active;
        contract.UpdatedAt = now;

        _context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = wallet.UserWalletsId,
            UserId = command.UserId,
            ContractsId = contract.ContractsId,
            ContractEscrowId = escrow.ContractEscrowId,
            TokenAmount = requiredTokens,
            VndAmount = requiredAmount,
            Type = (int)WalletTransactionType.EscrowHold,
            Status = (int)WalletTransactionStatus.Succeeded,
            GatewayProvider = "InternalTokenWallet",
            GatewayTransactionCode = $"ESCROW-HOLD-{escrow.ContractEscrowId:N}",
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
}
