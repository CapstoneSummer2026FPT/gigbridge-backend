using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Completion.Client.DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Services.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Completion.Client.Commands;

public sealed class EndProjectCommandHandler : IRequestHandler<EndProjectCommand, EndProjectResponse>
{
    private const string GatewayProvider = "InternalTokenWallet";

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public EndProjectCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<EndProjectResponse> Handle(
        EndProjectCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        var clientProfile = await ContractParticipantGuard.EnsureClientAsync(
            _context,
            contract,
            command.UserId,
            cancellationToken);

        if (contract.Status == (int)ContractStatus.Completed)
        {
            var completedEscrow = await GetEscrowAsync(contract.ContractsId, cancellationToken);
            return new EndProjectResponse(
                contract.ContractsId,
                contract.Status,
                0m,
                0m,
                completedEscrow.ReleasedAmount,
                contract.CompletedAt);
        }

        if (contract.Status != (int)ContractStatus.Active)
        {
            throw new BadRequestException("Only active contracts can be ended.");
        }

        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(
                profile => contract.FreelancerProfilesId.HasValue &&
                    profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                cancellationToken);

        if (freelancerProfile is null)
        {
            throw new BadRequestException("Contract does not have a selected freelancer.");
        }

        var milestones = await _context.Set<Milestone>()
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .OrderBy(milestone => milestone.SortOrder ?? int.MaxValue)
            .ThenBy(milestone => milestone.CreatedAt)
            .ToListAsync(cancellationToken);

        if (milestones.Count == 0)
        {
            throw new BadRequestException("Contract must have at least one milestone before ending the project.");
        }

        if (milestones.Any(milestone => milestone.Status != (int)MilestoneStatus.Approved))
        {
            throw new BadRequestException("All milestones must be approved before ending the project.");
        }

        var escrow = await GetEscrowAsync(contract.ContractsId, cancellationToken);
        if (escrow.Status == (int)ContractEscrowStatus.Disputed ||
            escrow.Status == (int)ContractEscrowStatus.Cancelled ||
            escrow.Status == (int)ContractEscrowStatus.Refunded)
        {
            throw new BadRequestException("Escrow is not eligible for final release.");
        }

        if (escrow.Status != (int)ContractEscrowStatus.Funded &&
            escrow.Status != (int)ContractEscrowStatus.PartiallyReleased &&
            escrow.Status != (int)ContractEscrowStatus.Released)
        {
            throw new BadRequestException("Escrow must be funded before ending the project.");
        }

        var totalMilestoneAmount = milestones.Sum(milestone => milestone.Amount);
        if (escrow.FundedAmount < totalMilestoneAmount)
        {
            throw new BadRequestException("Escrow funding is insufficient for the contract milestones.");
        }

        var remainingByMilestone = milestones
            .Select(milestone => new
            {
                Milestone = milestone,
                RemainingVnd = Math.Max(0m, milestone.Amount - milestone.ReleasedAmount)
            })
            .ToList();
        var releaseVnd = remainingByMilestone.Sum(item => item.RemainingVnd);
        var escrowRemainingVnd = escrow.FundedAmount - escrow.ReleasedAmount;

        if (escrowRemainingVnd < releaseVnd)
        {
            throw new BadRequestException("Escrow balance is inconsistent with milestone release amounts.");
        }

        var clientWallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(wallet => wallet.UserId == clientProfile.UserId, cancellationToken);

        if (clientWallet is null)
        {
            throw new BadRequestException("Client escrow wallet does not exist.");
        }

        var freelancerWallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(wallet => wallet.UserId == freelancerProfile.UserId, cancellationToken);

        var now = _dateTimeService.UtcNow;
        var releasedTokens = TokenWalletRules.ToTokens(releaseVnd);
        if (clientWallet.HeldTokens < releasedTokens)
        {
            throw new BadRequestException("Client held wallet balance is insufficient for final escrow release.");
        }

        if (freelancerWallet is null)
        {
            freelancerWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(),
                UserId = freelancerProfile.UserId,
                AvailableTokens = 0m,
                HeldTokens = 0m,
                CreatedAt = now
            };
            _context.Set<UserWallet>().Add(freelancerWallet);
        }

        if (releaseVnd > 0m)
        {
            clientWallet.HeldTokens -= releasedTokens;
            clientWallet.UpdatedAt = now;
            freelancerWallet.AvailableTokens += releasedTokens;
            freelancerWallet.UpdatedAt = now;
        }

        foreach (var release in remainingByMilestone)
        {
            var milestone = release.Milestone;
            var releaseTokens = TokenWalletRules.ToTokens(release.RemainingVnd);

            milestone.ReleasedAmount = milestone.Amount;
            milestone.LastReleasedAt = now;
            milestone.UpdatedAt = now;

            if (release.RemainingVnd <= 0m)
            {
                continue;
            }

            var transactionCode = $"ESCROW-FINAL-RELEASE-{escrow.ContractEscrowId:N}-{milestone.MilestonesId:N}";
            AddWalletTransactions(
                contract,
                escrow,
                milestone,
                clientWallet,
                freelancerWallet,
                release.RemainingVnd,
                releaseTokens,
                transactionCode,
                now);
            AddEscrowTransaction(escrow, milestone, release.RemainingVnd, transactionCode, now);
        }

        escrow.ReleasedAmount = escrow.FundedAmount;
        escrow.Status = (int)ContractEscrowStatus.Released;
        escrow.ReleasedAt = now;

        contract.Status = (int)ContractStatus.Completed;
        contract.CompletedAt = now;
        contract.UpdatedAt = now;

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            $"Final escrow released: {releaseVnd:N0} VND.",
            now,
            cancellationToken);
        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            "Contract completed. Reviews are now open.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var payload = new
        {
            contractId = contract.ContractsId,
            status = contract.Status,
            releasedAmountVnd = releaseVnd,
            releasedTokens,
            escrowReleasedAmountVnd = escrow.ReleasedAmount,
            completedAt = contract.CompletedAt
        };

        var conversationIds = await _context.Set<Conversation>()
            .Where(conversation => conversation.ContractsId == contract.ContractsId)
            .Select(conversation => conversation.ConversationsId)
            .ToListAsync(cancellationToken);

        foreach (var conversationId in conversationIds)
        {
            await _chatRealtimeNotifier.SendConversationEventAsync(
                conversationId,
                "ContractCompleted",
                payload,
                cancellationToken);
        }

        await _chatRealtimeNotifier.SendUsersEventAsync(
            new[] { clientProfile.UserId, freelancerProfile.UserId },
            "ContractCompleted",
            payload,
            cancellationToken);

        return new EndProjectResponse(
            contract.ContractsId,
            contract.Status,
            releaseVnd,
            releasedTokens,
            escrow.ReleasedAmount,
            contract.CompletedAt);
    }

    private async Task<ContractEscrow> GetEscrowAsync(Guid contractId, CancellationToken cancellationToken)
    {
        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(escrow => escrow.ContractsId == contractId, cancellationToken);

        return escrow ?? throw new NotFoundException("Contract escrow does not exist.");
    }

    private void AddWalletTransactions(
        Contract contract,
        ContractEscrow escrow,
        Milestone milestone,
        UserWallet clientWallet,
        UserWallet freelancerWallet,
        decimal releasedVnd,
        decimal releasedTokens,
        string transactionCode,
        DateTime now)
    {
        _context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = clientWallet.UserWalletsId,
            UserId = clientWallet.UserId,
            ContractsId = contract.ContractsId,
            ContractEscrowId = escrow.ContractEscrowId,
            MilestonesId = milestone.MilestonesId,
            TokenAmount = releasedTokens,
            VndAmount = releasedVnd,
            Type = (int)WalletTransactionType.EscrowRelease,
            Status = (int)WalletTransactionStatus.Succeeded,
            IdempotencyKey = transactionCode,
            GatewayProvider = GatewayProvider,
            GatewayTransactionCode = transactionCode,
            Note = "Final escrow release from client held wallet.",
            CreatedAt = now,
            CompletedAt = now
        });

        _context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = freelancerWallet.UserWalletsId,
            UserId = freelancerWallet.UserId,
            ContractsId = contract.ContractsId,
            ContractEscrowId = escrow.ContractEscrowId,
            MilestonesId = milestone.MilestonesId,
            TokenAmount = releasedTokens,
            VndAmount = releasedVnd,
            Type = (int)WalletTransactionType.EscrowRelease,
            Status = (int)WalletTransactionStatus.Succeeded,
            IdempotencyKey = transactionCode,
            GatewayProvider = GatewayProvider,
            GatewayTransactionCode = transactionCode,
            Note = "Final escrow release to freelancer wallet.",
            CreatedAt = now,
            CompletedAt = now
        });
    }

    private void AddEscrowTransaction(
        ContractEscrow escrow,
        Milestone milestone,
        decimal releasedVnd,
        string transactionCode,
        DateTime now)
    {
        _context.Set<EscrowTransaction>().Add(new EscrowTransaction
        {
            EscrowTransactionId = Guid.NewGuid(),
            ContractEscrowId = escrow.ContractEscrowId,
            MilestonesId = milestone.MilestonesId,
            Amount = releasedVnd,
            Type = (int)EscrowTransactionType.ReleaseToFreelancer,
            Status = (int)EscrowTransactionStatus.Succeeded,
            PaymentGateway = GatewayProvider,
            GatewayTransactionCode = transactionCode,
            Note = "Final project completion escrow release.",
            CreatedAt = now,
            CompletedAt = now
        });
    }
}
