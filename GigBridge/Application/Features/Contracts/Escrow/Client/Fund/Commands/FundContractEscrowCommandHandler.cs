using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Escrow.Client.Fund.DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Services.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Escrow.Client.Fund.Commands;

public sealed class FundContractEscrowCommandHandler :
    IRequestHandler<FundContractEscrowCommand, FundContractEscrowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public FundContractEscrowCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
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

        if (contract.Status == (int)ContractStatus.PendingSignature ||
            contract.Status == (int)ContractStatus.Active)
        {
            var fundedEscrow = await GetEscrowAsync(contract.ContractsId, cancellationToken);
            return new FundContractEscrowResponse(
                contract.ContractsId,
                fundedEscrow.ContractEscrowId,
                fundedEscrow.RequiredAmount,
                TokenWalletRules.ToTokensCeiling(fundedEscrow.RequiredAmount),
                contract.Status,
                fundedEscrow.Status);
        }

        if (contract.Status != (int)ContractStatus.PendingEscrow)
        {
            throw new BadRequestException("Contract escrow can only be funded after details are confirmed.");
        }

        var escrow = await GetEscrowAsync(contract.ContractsId, cancellationToken);
        if (escrow.Status == (int)ContractEscrowStatus.Funded)
        {
            contract.Status = (int)ContractStatus.PendingSignature;
            await _context.SaveChangesAsync(cancellationToken);
            return new FundContractEscrowResponse(
                contract.ContractsId,
                escrow.ContractEscrowId,
                escrow.RequiredAmount,
                TokenWalletRules.ToTokensCeiling(escrow.RequiredAmount),
                contract.Status,
                escrow.Status);
        }

        var wallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(wallet => wallet.UserId == command.UserId, cancellationToken);

        if (wallet is null)
        {
            throw new BadRequestException("Wallet balance is insufficient to fund escrow.");
        }

        var requiredVnd = escrow.RequiredAmount - escrow.FundedAmount;
        var requiredTokens = TokenWalletRules.ToTokensCeiling(requiredVnd);
        if (wallet.AvailableTokens < requiredTokens)
        {
            throw new BadRequestException("Wallet balance is insufficient to fund escrow.");
        }

        var now = _dateTimeService.UtcNow;
        await ContractEsignRenderer.EnsureDocumentAsync(_context, contract, now, cancellationToken);

        wallet.AvailableTokens -= requiredTokens;
        wallet.HeldTokens += requiredTokens;
        wallet.UpdatedAt = now;

        escrow.FundedAmount = escrow.RequiredAmount;
        escrow.Status = (int)ContractEscrowStatus.Funded;
        escrow.FundedAt = now;

        contract.Status = (int)ContractStatus.PendingSignature;
        contract.UpdatedAt = now;

        _context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = wallet.UserWalletsId,
            UserId = command.UserId,
            ContractsId = contract.ContractsId,
            ContractEscrowId = escrow.ContractEscrowId,
            TokenAmount = requiredTokens,
            VndAmount = requiredVnd,
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
            Amount = requiredVnd,
            Type = (int)EscrowTransactionType.Deposit,
            Status = (int)EscrowTransactionStatus.Succeeded,
            PaymentGateway = "InternalTokenWallet",
            GatewayTransactionCode = $"ESCROW-HOLD-{escrow.ContractEscrowId:N}",
            Note = "Funded from client token wallet.",
            CreatedAt = now,
            CompletedAt = now
        });

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            "Escrow funded from wallet. Contract is ready for signatures.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

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
