using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using Domain.Services.Payments;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Common;

public sealed class WalletLedgerService : IWalletLedgerService
{
    private readonly GigbridgeDbContext _context;
    private readonly IDateTimeService _clock;

    public WalletLedgerService(GigbridgeDbContext context, IDateTimeService clock)
    {
        _context = context;
        _clock = clock;
    }

    public Task<WalletTransaction> DebitAsync(
        Guid userId,
        decimal tokenAmount,
        WalletTransactionType type,
        string idempotencyKey,
        string? metadata,
        CancellationToken cancellationToken) =>
        ApplyAsync(userId, tokenAmount, type, idempotencyKey, metadata, cancellationToken);

    private async Task<WalletTransaction> ApplyAsync(
        Guid userId,
        decimal tokenAmount,
        WalletTransactionType type,
        string idempotencyKey,
        string? metadata,
        CancellationToken cancellationToken)
    {
        if (tokenAmount <= 0)
            throw new BadRequestException("Token amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new BadRequestException("An idempotency key is required.");

        var existing = await _context.WalletTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.Type != (int)type ||
                existing.TokenAmount != tokenAmount ||
                existing.Metadata != metadata)
                throw new ConflictException("The idempotency key was already used for a different wallet operation.");
            return existing;
        }

        var ownsTransaction = _context.Database.CurrentTransaction is null;
        await using var dbTransaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var wallet = await _context.UserWallets
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Wallet does not exist.");

        var now = _clock.UtcNow;
        var affected = await _context.UserWallets
            .Where(x => x.UserWalletsId == wallet.UserWalletsId &&
                        x.Version == wallet.Version &&
                        x.AvailableTokens >= tokenAmount)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.AvailableTokens, x => x.AvailableTokens - tokenAmount)
                .SetProperty(
                    x => x.WithdrawableTokens,
                    x => x.WithdrawableTokens -
                        (tokenAmount > x.AvailableTokens - x.WithdrawableTokens
                            ? tokenAmount - (x.AvailableTokens - x.WithdrawableTokens)
                            : 0m))
                .SetProperty(x => x.Version, x => x.Version + 1)
                .SetProperty(x => x.UpdatedAt, now), cancellationToken);

        if (affected != 1)
        {
            _context.ChangeTracker.Clear();
            var balance = await _context.UserWallets
                .AsNoTracking()
                .Where(x => x.UserWalletsId == wallet.UserWalletsId)
                .Select(x => x.AvailableTokens)
                .SingleAsync(cancellationToken);
            if (balance < tokenAmount)
                throw new BadRequestException("Insufficient wallet balance.");
            throw new ConflictException("The wallet changed concurrently. Retry the operation with the same idempotency key.");
        }

        var transaction = new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = wallet.UserWalletsId,
            UserId = userId,
            TokenAmount = tokenAmount,
            VndAmount = 0,
            Type = (int)type,
            Status = (int)WalletTransactionStatus.Succeeded,
            IdempotencyKey = idempotencyKey,
            Metadata = metadata,
            CreatedAt = now,
            CompletedAt = now
        };
        _context.WalletTransactions.Add(transaction);

        if (type is WalletTransactionType.SubscriptionPurchase or WalletTransactionType.PromotionPurchase)
        {
            var revenueSource = ResolveRevenueSource(type, idempotencyKey, metadata);
            _context.PlatformRevenueEvents.Add(new PlatformRevenueEvent
            {
                PlatformRevenueEventId = Guid.NewGuid(),
                Source = revenueSource,
                WalletTransactionId = transaction.WalletTransactionsId,
                PayerUserId = userId,
                SourceEntityType = nameof(WalletTransaction),
                SourceEntityId = transaction.WalletTransactionsId,
                SourceReference = idempotencyKey,
                GigCoinAmount = tokenAmount,
                VndEquivalent = TokenWalletRules.ToVnd(tokenAmount),
                VndPerGigCoin = TokenWalletRules.VndPerToken,
                OccurredAt = now,
                RecordedAt = now,
                Metadata = metadata
            });

            if (type == WalletTransactionType.PromotionPurchase)
            {
                _context.PremiumUsageEvents.Add(new PremiumUsageEvent
                {
                    PremiumUsageEventId = Guid.NewGuid(),
                    Type = revenueSource switch
                    {
                        PlatformRevenueSource.JobPromotionPurchase => PremiumUsageEventType.JobPromotion,
                        PlatformRevenueSource.PromotionBoost => PremiumUsageEventType.ProfilePromotionBoost,
                        _ => PremiumUsageEventType.ProfilePromotion
                    },
                    UserId = userId,
                    IdempotencyKey = $"wallet:{transaction.WalletTransactionsId:N}:premium-usage",
                    OccurredAt = now,
                    Metadata = metadata
                });
            }
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (dbTransaction is not null)
                await dbTransaction.CommitAsync(cancellationToken);
            return transaction;
        }
        catch (DbUpdateException)
        {
            if (dbTransaction is not null)
                await dbTransaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            existing = await _context.WalletTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null &&
                existing.Type == (int)type &&
                existing.TokenAmount == tokenAmount &&
                existing.Metadata == metadata)
                return existing;
            throw;
        }
    }

    private static PlatformRevenueSource ResolveRevenueSource(
        WalletTransactionType type,
        string idempotencyKey,
        string? metadata)
    {
        if (type == WalletTransactionType.SubscriptionPurchase)
            return PlatformRevenueSource.SubscriptionPurchase;
        if (idempotencyKey.StartsWith("job-promotion:", StringComparison.OrdinalIgnoreCase))
            return PlatformRevenueSource.JobPromotionPurchase;
        if (metadata?.Contains("boostTokenAmount", StringComparison.OrdinalIgnoreCase) == true)
            return PlatformRevenueSource.PromotionBoost;
        return PlatformRevenueSource.ProfilePromotionPurchase;
    }
}
