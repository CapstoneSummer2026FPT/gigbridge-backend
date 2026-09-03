using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Wallets.Interfaces;
using Application.Common.Options;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Wallets;
using Domain.Services.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Application.Features.Wallets.Common.Withdrawals.Create;

public sealed class CreateWithdrawalCommandHandler :
    IRequestHandler<CreateWithdrawalCommand, WithdrawalResponse>
{
    private const int MaxWalletRetries = 3;
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly WalletWithdrawalOptions _options;
    private readonly IBankAccountProtector _bankAccountProtector;
    private readonly IPayoutProvider _payoutProvider;
    private readonly ILogger<CreateWithdrawalCommandHandler> _logger;

    public CreateWithdrawalCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IOptions<WalletWithdrawalOptions> options,
        IBankAccountProtector bankAccountProtector,
        IPayoutProvider payoutProvider,
        ILogger<CreateWithdrawalCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _options = options.Value;
        _bankAccountProtector = bankAccountProtector;
        _payoutProvider = payoutProvider;
        _logger = logger;
    }

    public async Task<WithdrawalResponse> Handle(
        CreateWithdrawalCommand command,
        CancellationToken cancellationToken)
    {
        var tokenAmount = decimal.Round(command.Request.TokenAmount, 4, MidpointRounding.AwayFromZero);
        var idempotencyKey = command.Request.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
        {
            throw new BadRequestException("A valid idempotency key is required.");
        }

        var existing = await _context.Set<WalletWithdrawal>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                withdrawal => withdrawal.UserId == command.UserId &&
                    withdrawal.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.TokenAmount != tokenAmount ||
                (command.Request.BankAccountId.HasValue && existing.BankAccountId != command.Request.BankAccountId))
            {
                throw new ConflictException("Idempotency key was already used for another withdrawal payload.");
            }

            return WithdrawalResponse.FromEntity(existing);
        }

        if (!_options.Enabled)
        {
            throw new ConflictException("Bank withdrawals are temporarily disabled.");
        }

        if (tokenAmount <= 0 || tokenAmount < _options.MinTokens || tokenAmount > _options.MaxTokens)
        {
            throw new BadRequestException($"Withdrawal amount must be between {_options.MinTokens} and {_options.MaxTokens} tokens.");
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

        var bankAccount = command.Request.BankAccountId.HasValue
            ? await _context.Set<BankAccount>().AsNoTracking().FirstOrDefaultAsync(
                account => account.BankAccountId == command.Request.BankAccountId.Value &&
                    account.UserId == command.UserId && account.DeletedAt == null &&
                    account.Status == (int)BankAccountStatus.Active,
                cancellationToken)
            : await _context.Set<BankAccount>().AsNoTracking()
                .Where(account => account.UserId == command.UserId && account.DeletedAt == null &&
                    account.Status == (int)BankAccountStatus.Active)
                .OrderByDescending(account => account.IsDefault)
                .ThenByDescending(account => account.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        if (bankAccount is null || string.IsNullOrWhiteSpace(bankAccount.BankBin))
        {
            throw new BadRequestException("An active bank account with a valid BIN is required before withdrawal.");
        }

        try
        {
            _bankAccountProtector.Unprotect(bankAccount.AccountNumberEncrypted);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(
                ex,
                "Bank account {BankAccountId} for user {UserId} could not be decrypted and was disabled. " +
                "A data-protection key rotation would disable accounts in bulk.",
                bankAccount.BankAccountId,
                command.UserId);
            var invalidAccount = await _context.Set<BankAccount>().FirstAsync(
                account => account.BankAccountId == bankAccount.BankAccountId,
                cancellationToken);
            invalidAccount.Status = (int)BankAccountStatus.Disabled;
            invalidAccount.IsDefault = false;
            invalidAccount.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
            throw new BadRequestException("Bank account encryption is no longer valid. Please add the account again.");
        }

        var vndAmount = decimal.Round(TokenWalletRules.ToVnd(tokenAmount), 0, MidpointRounding.AwayFromZero);
        var feeVnd = decimal.Round(_options.FixedFeeVnd, 0, MidpointRounding.AwayFromZero);
        var netVndAmount = vndAmount - feeVnd;
        if (netVndAmount <= 0)
        {
            throw new BadRequestException("Withdrawal amount must be greater than the payout fee.");
        }

        // A provider outage must not block queueing a withdrawal - the payout outbox exists
        // precisely so the request survives one. Only a readable-but-insufficient payout balance
        // is grounds to refuse, because that request is certain to fail.
        var availability = await _payoutProvider.CheckAvailabilityAsync(cancellationToken);
        if (!availability.IsAvailable)
        {
            _logger.LogWarning(
                "Payout provider is unavailable while user {UserId} requested a withdrawal of " +
                "{NetVndAmount} VND; queueing it for retry. ErrorCode={ErrorCode} Reason={Reason}",
                command.UserId,
                netVndAmount,
                availability.ErrorCode,
                availability.SafeMessage);
        }
        else if (!availability.BalanceVnd.HasValue || availability.BalanceVnd.Value < netVndAmount)
        {
            _logger.LogWarning(
                "Refusing withdrawal for user {UserId}: payout account balance {BalanceVnd} VND is " +
                "below the requested {NetVndAmount} VND.",
                command.UserId,
                availability.BalanceVnd,
                netVndAmount);
            throw new ExternalServiceException(
                "Bank withdrawals are temporarily unavailable because the payout account balance is insufficient.");
        }

        var withdrawalId = Guid.NewGuid();
        for (var attempt = 0; attempt < MaxWalletRetries; attempt++)
        {
            try
            {
                await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
                var usedToday = await _context.Set<WalletWithdrawal>()
                    .Where(withdrawal => withdrawal.UserId == command.UserId &&
                        withdrawal.CreatedAt >= now.Date &&
                        withdrawal.Status != (int)WithdrawalStatus.Failed &&
                        withdrawal.Status != (int)WithdrawalStatus.Cancelled)
                    .SumAsync(withdrawal => (decimal?)withdrawal.TokenAmount, cancellationToken) ?? 0m;
                if (usedToday + tokenAmount > _options.DailyMaxTokens)
                {
                    throw new BadRequestException("Daily withdrawal limit exceeded.");
                }

                var wallet = await _context.Set<UserWallet>().AsNoTracking()
                    .FirstOrDefaultAsync(item => item.UserId == command.UserId, cancellationToken);
                if (wallet is null || wallet.WithdrawableTokens < tokenAmount)
                {
                    throw new BadRequestException("Withdrawable project earnings are insufficient for withdrawal.");
                }

                if (!await TryLockWalletAsync(wallet, tokenAmount, now, cancellationToken))
                {
                    continue;
                }

                var providerOrderCode = $"wd_{withdrawalId:N}";
                var withdrawal = new WalletWithdrawal
                {
                    WalletWithdrawalId = withdrawalId,
                    UserWalletsId = wallet.UserWalletsId,
                    UserId = command.UserId,
                    BankAccountId = bankAccount.BankAccountId,
                    BankBin = bankAccount.BankBin,
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
                    ProviderOrderCode = providerOrderCode,
                    IdempotencyKey = idempotencyKey,
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
                    BalanceSource = (int)WalletBalanceSource.Earned,
                    EarnedAmount = tokenAmount,
                    Type = (int)WalletTransactionType.WithdrawalLock,
                    Status = (int)WalletTransactionStatus.Succeeded,
                    IdempotencyKey = $"withdrawal:{withdrawalId:D}:lock",
                    GatewayProvider = "InternalTokenWallet",
                    GatewayOrderCode = providerOrderCode,
                    GatewayTransactionCode = $"WITHDRAWAL-LOCK-{withdrawalId:N}",
                    Metadata = withdrawalId.ToString("D"),
                    Note = "Locked earned wallet balance for payout withdrawal.",
                    CreatedAt = now,
                    CompletedAt = now
                });
                _context.Set<PayoutOutbox>().Add(new PayoutOutbox
                {
                    PayoutOutboxId = Guid.NewGuid(),
                    WalletWithdrawalId = withdrawalId,
                    PayoutKey = $"withdrawal:{withdrawalId:D}:create",
                    Status = (int)PayoutOutboxStatus.Pending,
                    AttemptCount = 0,
                    NextAttemptAt = now,
                    CreatedAt = now
                });

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return WithdrawalResponse.FromEntity(withdrawal);
            }
            catch (DbUpdateException)
            {
                var racedWithdrawal = await _context.Set<WalletWithdrawal>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        withdrawal => withdrawal.UserId == command.UserId &&
                            withdrawal.IdempotencyKey == idempotencyKey,
                        cancellationToken);
                if (racedWithdrawal is null)
                {
                    throw;
                }

                if (racedWithdrawal.TokenAmount != tokenAmount ||
                    (command.Request.BankAccountId.HasValue && racedWithdrawal.BankAccountId != command.Request.BankAccountId))
                {
                    throw new ConflictException("Idempotency key was already used for another withdrawal payload.");
                }

                return WithdrawalResponse.FromEntity(racedWithdrawal);
            }
        }

        throw new ConflictException("Wallet balance changed during withdrawal. Please try again.");
    }

    private async Task<bool> TryLockWalletAsync(
        UserWallet snapshot,
        decimal tokenAmount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _context.Set<UserWallet>()
                .Where(wallet => wallet.UserWalletsId == snapshot.UserWalletsId &&
                    wallet.Version == snapshot.Version && wallet.WithdrawableTokens >= tokenAmount)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(wallet => wallet.WithdrawableTokens, wallet => wallet.WithdrawableTokens - tokenAmount)
                        .SetProperty(wallet => wallet.PendingWithdrawalTokens, wallet => wallet.PendingWithdrawalTokens + tokenAmount)
                        .SetProperty(wallet => wallet.Version, wallet => wallet.Version + 1)
                        .SetProperty(wallet => wallet.UpdatedAt, now),
                    cancellationToken) == 1;
        }
        catch (Exception ex) when (IsExecuteUpdateUnsupported(ex))
        {
            var wallet = await _context.Set<UserWallet>()
                .FirstOrDefaultAsync(item => item.UserWalletsId == snapshot.UserWalletsId, cancellationToken);
            if (wallet is null || wallet.Version != snapshot.Version ||
                wallet.WithdrawableTokens < tokenAmount)
            {
                return false;
            }

            wallet.WithdrawableTokens -= tokenAmount;
            wallet.PendingWithdrawalTokens += tokenAmount;
            wallet.Version++;
            wallet.UpdatedAt = now;
            return true;
        }
    }

    private static bool IsExecuteUpdateUnsupported(Exception exception) =>
        exception is InvalidOperationException or NotSupportedException ||
        exception.InnerException is not null && IsExecuteUpdateUnsupported(exception.InnerException);
}
