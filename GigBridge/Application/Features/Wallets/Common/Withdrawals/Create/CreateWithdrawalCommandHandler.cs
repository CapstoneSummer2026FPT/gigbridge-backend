using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Options;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Services.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Application.Features.Wallets.Common.Withdrawals.Create;

public sealed class CreateWithdrawalCommandHandler :
    IRequestHandler<CreateWithdrawalCommand, WithdrawalResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly WalletWithdrawalOptions _options;

    public CreateWithdrawalCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IOptions<WalletWithdrawalOptions> options)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _options = options.Value;
    }

    public async Task<WithdrawalResponse> Handle(
        CreateWithdrawalCommand command,
        CancellationToken cancellationToken)
    {
        var tokenAmount = decimal.Round(command.Request.TokenAmount, 4, MidpointRounding.AwayFromZero);
        if (tokenAmount <= 0)
        {
            throw new BadRequestException("Withdrawal amount must be greater than zero.");
        }

        if (tokenAmount < _options.MinTokens || tokenAmount > _options.MaxTokens)
        {
            throw new BadRequestException($"Withdrawal amount must be between {_options.MinTokens} and {_options.MaxTokens} tokens.");
        }

        if (!string.IsNullOrWhiteSpace(command.Request.IdempotencyKey))
        {
            var existing = await _context.Set<WalletWithdrawal>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    withdrawal =>
                        withdrawal.UserId == command.UserId &&
                        withdrawal.IdempotencyKey == command.Request.IdempotencyKey,
                    cancellationToken);

            if (existing is not null)
            {
                return WithdrawalResponse.FromEntity(existing);
            }
        }

        var now = _dateTimeService.UtcNow;
        var user = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserId == command.UserId, cancellationToken);

        if (user is null || user.Role != (int)UserRole.Freelancer)
        {
            throw new ForbiddenAccessException("Only freelancers can create withdrawal requests.");
        }

        if (!user.IsActive || (user.SuspendedUntil.HasValue && user.SuspendedUntil.Value > now))
        {
            throw new ForbiddenAccessException("Your account is not allowed to withdraw at this time.");
        }

        var dailyStart = now.Date;
        var usedToday = await _context.Set<WalletWithdrawal>()
            .Where(withdrawal =>
                withdrawal.UserId == command.UserId &&
                withdrawal.CreatedAt >= dailyStart &&
                withdrawal.Status != (int)WithdrawalStatus.Failed &&
                withdrawal.Status != (int)WithdrawalStatus.Cancelled)
            .SumAsync(withdrawal => (decimal?)withdrawal.TokenAmount, cancellationToken) ?? 0m;

        if (usedToday + tokenAmount > _options.DailyMaxTokens)
        {
            throw new BadRequestException("Daily withdrawal limit exceeded.");
        }

        var bankAccount = command.Request.BankAccountId.HasValue
            ? await _context.Set<BankAccount>().FirstOrDefaultAsync(
                account =>
                    account.BankAccountId == command.Request.BankAccountId.Value &&
                    account.UserId == command.UserId &&
                    account.DeletedAt == null &&
                    account.Status == (int)BankAccountStatus.Active,
                cancellationToken)
            : await _context.Set<BankAccount>()
                .Where(account =>
                    account.UserId == command.UserId &&
                    account.DeletedAt == null &&
                    account.Status == (int)BankAccountStatus.Active)
                .OrderByDescending(account => account.IsDefault)
                .ThenByDescending(account => account.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        if (bankAccount is null)
        {
            throw new BadRequestException("An active bank account is required before withdrawal.");
        }

        var wallet = await _context.Set<UserWallet>()
            .AsNoTracking()
            .FirstOrDefaultAsync(wallet => wallet.UserId == command.UserId, cancellationToken);

        if (wallet is null || wallet.AvailableTokens < tokenAmount || wallet.WithdrawableTokens < tokenAmount)
        {
            throw new BadRequestException("Withdrawable project earnings are insufficient for withdrawal.");
        }

        var vndAmount = TokenWalletRules.ToVnd(tokenAmount);
        var feeVnd = decimal.Round(_options.FixedFeeVnd, 2, MidpointRounding.AwayFromZero);
        var netVndAmount = vndAmount - feeVnd;
        if (netVndAmount <= 0)
        {
            throw new BadRequestException("Withdrawal amount must be greater than the payout fee.");
        }

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        var locked = await TryLockWalletAsync(wallet.UserWalletsId, tokenAmount, now, cancellationToken);
        if (!locked)
        {
            throw new BadRequestException("Withdrawable project earnings are insufficient for withdrawal.");
        }

        var withdrawal = new WalletWithdrawal
        {
            WalletWithdrawalId = Guid.NewGuid(),
            UserWalletsId = wallet.UserWalletsId,
            UserId = command.UserId,
            BankAccountId = bankAccount.BankAccountId,
            BankCode = bankAccount.BankCode,
            BankName = bankAccount.BankName,
            BankAccountNumberEncrypted = bankAccount.AccountNumberEncrypted,
            BankAccountNumberMasked = bankAccount.AccountNumberMasked,
            BankAccountName = bankAccount.AccountName,
            TokenAmount = tokenAmount,
            VndAmount = vndAmount,
            FeeVnd = feeVnd,
            NetVndAmount = netVndAmount,
            Status = (int)WithdrawalStatus.Pending,
            Provider = _options.Provider,
            ProviderOrderCode = GenerateProviderOrderCode(now),
            IdempotencyKey = command.Request.IdempotencyKey,
            CreatedAt = now
        };

        _context.Set<WalletWithdrawal>().Add(withdrawal);
        _context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = wallet.UserWalletsId,
            UserId = command.UserId,
            TokenAmount = tokenAmount,
            VndAmount = vndAmount,
            Type = (int)WalletTransactionType.WithdrawalLock,
            Status = (int)WalletTransactionStatus.Succeeded,
            GatewayProvider = "InternalTokenWallet",
            GatewayOrderCode = withdrawal.ProviderOrderCode,
            GatewayTransactionCode = $"WITHDRAWAL-LOCK-{withdrawal.WalletWithdrawalId:N}",
            Metadata = withdrawal.WalletWithdrawalId.ToString("D"),
            Note = "Locked wallet balance for payout withdrawal.",
            CreatedAt = now,
            CompletedAt = now
        });
        _context.Set<PayoutOutbox>().Add(new PayoutOutbox
        {
            PayoutOutboxId = Guid.NewGuid(),
            WalletWithdrawalId = withdrawal.WalletWithdrawalId,
            PayoutKey = $"withdrawal:{withdrawal.WalletWithdrawalId:D}:create",
            Status = (int)PayoutOutboxStatus.Pending,
            AttemptCount = 0,
            NextAttemptAt = now,
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return WithdrawalResponse.FromEntity(withdrawal);
    }

    private async Task<bool> TryLockWalletAsync(
        Guid walletId,
        decimal tokenAmount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _context.Set<UserWallet>()
                .Where(wallet =>
                    wallet.UserWalletsId == walletId &&
                    wallet.AvailableTokens >= tokenAmount &&
                    wallet.WithdrawableTokens >= tokenAmount)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(wallet => wallet.AvailableTokens, wallet => wallet.AvailableTokens - tokenAmount)
                        .SetProperty(wallet => wallet.WithdrawableTokens, wallet => wallet.WithdrawableTokens - tokenAmount)
                        .SetProperty(wallet => wallet.PendingWithdrawalTokens, wallet => wallet.PendingWithdrawalTokens + tokenAmount)
                        .SetProperty(wallet => wallet.UpdatedAt, now),
                    cancellationToken);

            return updated == 1;
        }
        catch (Exception ex) when (IsExecuteUpdateUnsupported(ex))
        {
            var wallet = await _context.Set<UserWallet>()
                .FirstOrDefaultAsync(wallet => wallet.UserWalletsId == walletId, cancellationToken);

            if (wallet is null || wallet.AvailableTokens < tokenAmount || wallet.WithdrawableTokens < tokenAmount)
            {
                return false;
            }

            wallet.AvailableTokens -= tokenAmount;
            wallet.WithdrawableTokens -= tokenAmount;
            wallet.PendingWithdrawalTokens += tokenAmount;
            wallet.UpdatedAt = now;
            return true;
        }
    }

    private static bool IsExecuteUpdateUnsupported(Exception exception)
    {
        if (exception is InvalidOperationException or NotSupportedException)
        {
            return true;
        }

        return exception.InnerException is not null && IsExecuteUpdateUnsupported(exception.InnerException);
    }

    private static string GenerateProviderOrderCode(DateTime now)
    {
        return $"WD{now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
    }
}
