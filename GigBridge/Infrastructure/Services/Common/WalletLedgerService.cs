using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
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
        ApplyAsync(userId, tokenAmount, type, idempotencyKey, metadata, isCredit: false, cancellationToken);

    public Task<WalletTransaction> CreditAsync(
        Guid userId,
        decimal tokenAmount,
        WalletTransactionType type,
        string idempotencyKey,
        string? metadata,
        CancellationToken cancellationToken) =>
        ApplyAsync(userId, tokenAmount, type, idempotencyKey, metadata, isCredit: true, cancellationToken);

    private async Task<WalletTransaction> ApplyAsync(
        Guid userId,
        decimal tokenAmount,
        WalletTransactionType type,
        string idempotencyKey,
        string? metadata,
        bool isCredit,
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
        var affected = isCredit
            ? await _context.UserWallets
                .Where(x => x.UserWalletsId == wallet.UserWalletsId && x.Version == wallet.Version)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.AvailableTokens, x => x.AvailableTokens + tokenAmount)
                    .SetProperty(x => x.Version, x => x.Version + 1)
                    .SetProperty(x => x.UpdatedAt, now), cancellationToken)
            : await _context.UserWallets
                .Where(x => x.UserWalletsId == wallet.UserWalletsId &&
                            x.Version == wallet.Version &&
                            x.AvailableTokens >= tokenAmount)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.AvailableTokens, x => x.AvailableTokens - tokenAmount)
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
            if (!isCredit && balance < tokenAmount)
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
}
