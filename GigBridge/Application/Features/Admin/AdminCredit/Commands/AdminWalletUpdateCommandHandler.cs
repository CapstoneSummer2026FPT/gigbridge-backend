using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Wallets.Common;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Services.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.AdminCredit.Commands;

public sealed class AdminWalletUpdateCommandHandler :
    IRequestHandler<AdminWalletUpdateCommand, WalletTransactionResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public AdminWalletUpdateCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<WalletTransactionResponse> Handle(
        AdminWalletUpdateCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Request.TokenAmount <= 0)
        {
            throw new BadRequestException("Token amount must be greater than zero.");
        }

        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == command.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can update wallet tokens.");
        }

        var targetUserExists = await _context.Set<User>()
            .AnyAsync(user => user.UserId == command.TargetUserId, cancellationToken);

        if (!targetUserExists)
        {
            throw new NotFoundException("Target user does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(command.Request.IdempotencyKey))
        {
            var existing = await _context.Set<WalletTransaction>()
                .FirstOrDefaultAsync(
                    transaction =>
                        transaction.UserId == command.TargetUserId &&
                        transaction.IdempotencyKey == command.Request.IdempotencyKey,
                    cancellationToken);

            if (existing is not null)
            {
                return WalletTransactionResponse.FromEntity(existing);
            }
        }

        var now = _dateTimeService.UtcNow;
        var wallet = await WalletWorkflow.GetOrCreateWalletAsync(
            _context,
            command.TargetUserId,
            now,
            cancellationToken);

        var walletBefore = WalletBalanceAudit.Snapshot(wallet);
        var usage = WalletWorkflow.DebitAvailable(
            wallet,
            command.Request.TokenAmount,
            now,
            "Insufficient wallet balance for debit operation.");
        var (depositedAmount, earnedAmount) = WalletBalanceAudit.ToSplitAmounts(
            usage.DepositedAmount,
            usage.EarnedAmount);

        var transaction = new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = wallet.UserWalletsId,
            UserId = command.TargetUserId,
            TokenAmount = command.Request.TokenAmount,
            VndAmount = TokenWalletRules.ToVnd(command.Request.TokenAmount),
            BalanceSource = (int)WalletBalanceAudit.ResolveSource(usage.DepositedAmount, usage.EarnedAmount),
            DepositedAmount = depositedAmount,
            EarnedAmount = earnedAmount,
            Type = (int)WalletTransactionType.Adjustment,
            Status = (int)WalletTransactionStatus.Succeeded,
            IdempotencyKey = command.Request.IdempotencyKey,
            GatewayProvider = "Admin",
            Metadata = WalletBalanceAudit.EnrichMetadata(
                null, usage.DepositedAmount, usage.EarnedAmount, walletBefore, wallet),
            Note = command.Request.Note,
            CreatedAt = now,
            CompletedAt = now
        };

        _context.Set<WalletTransaction>().Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        return WalletTransactionResponse.FromEntity(transaction);
    }
}
