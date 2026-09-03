using Application.Common.Interfaces;
using Application.Features.Wallets.Common.DTOs;
using Application.Features.Wallets.Common.Withdrawals.Sync;
using Domain.Entities;
using Domain.Enums.Wallets;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

/// <summary>
/// Re-reads provider state for every non-terminal withdrawal. This is the safe first move after
/// fixing a payout outage: it only reads from the provider, so it can never pay twice, and it
/// separates withdrawals the provider already paid from ones it never received.
/// </summary>
public sealed class BulkSyncWithdrawalsCommandHandler
    : IRequestHandler<BulkSyncWithdrawalsCommand, BulkWithdrawalOperationResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly ILogger<BulkSyncWithdrawalsCommandHandler> _logger;

    public BulkSyncWithdrawalsCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        ILogger<BulkSyncWithdrawalsCommandHandler> logger)
    {
        _context = context;
        _sender = sender;
        _logger = logger;
    }

    public async Task<BulkWithdrawalOperationResponse> Handle(
        BulkSyncWithdrawalsCommand command,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(command.Limit, 1, 100);
        var query = _context.Set<WalletWithdrawal>()
            .AsNoTracking()
            .Where(withdrawal =>
                withdrawal.Status != (int)WithdrawalStatus.Success &&
                withdrawal.Status != (int)WithdrawalStatus.Failed &&
                withdrawal.Status != (int)WithdrawalStatus.Cancelled);

        if (command.Status.HasValue)
        {
            query = query.Where(withdrawal => withdrawal.Status == command.Status.Value);
        }

        var withdrawalIds = await query
            .OrderBy(withdrawal => withdrawal.CreatedAt)
            .Select(withdrawal => withdrawal.WalletWithdrawalId)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Admin bulk sync starting for {Count} withdrawal(s). StatusFilter={StatusFilter}",
            withdrawalIds.Count,
            command.Status);

        var items = new List<BulkWithdrawalItemResult>(withdrawalIds.Count);
        foreach (var withdrawalId in withdrawalIds)
        {
            try
            {
                var result = await _sender.Send(
                    new SyncWithdrawalCommand(withdrawalId, null, true),
                    cancellationToken);
                items.Add(new BulkWithdrawalItemResult(withdrawalId, true, result.Status, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Admin bulk sync failed for withdrawal {WithdrawalId}.", withdrawalId);
                items.Add(new BulkWithdrawalItemResult(
                    withdrawalId,
                    false,
                    null,
                    $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        return Summarize(items);
    }

    internal static BulkWithdrawalOperationResponse Summarize(
        IReadOnlyList<BulkWithdrawalItemResult> items)
    {
        var succeeded = items.Count(item => item.Success);
        return new BulkWithdrawalOperationResponse(
            items.Count,
            succeeded,
            items.Count - succeeded,
            items);
    }
}
