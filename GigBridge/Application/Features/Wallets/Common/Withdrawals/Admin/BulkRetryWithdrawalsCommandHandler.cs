using Application.Common.Exceptions;
using Application.Features.Wallets.Common.DTOs;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

/// <summary>
/// Re-queues an explicit list of withdrawals for payout. Takes ids rather than a filter on purpose:
/// retrying re-sends money, so the set has to be chosen deliberately - normally the rows a bulk
/// sync just proved the provider never received. Each id goes through
/// <see cref="RetryWithdrawalCommandHandler"/>, keeping its guards and its audit-log entry.
/// </summary>
public sealed class BulkRetryWithdrawalsCommandHandler
    : IRequestHandler<BulkRetryWithdrawalsCommand, BulkWithdrawalOperationResponse>
{
    private const int MaxBatchSize = 100;

    private readonly ISender _sender;
    private readonly ILogger<BulkRetryWithdrawalsCommandHandler> _logger;

    public BulkRetryWithdrawalsCommandHandler(
        ISender sender,
        ILogger<BulkRetryWithdrawalsCommandHandler> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task<BulkWithdrawalOperationResponse> Handle(
        BulkRetryWithdrawalsCommand command,
        CancellationToken cancellationToken)
    {
        var withdrawalIds = command.WithdrawalIds?.Distinct().ToList() ?? [];
        if (withdrawalIds.Count == 0)
        {
            throw new BadRequestException("At least one withdrawal id is required.");
        }

        if (withdrawalIds.Count > MaxBatchSize)
        {
            throw new BadRequestException($"At most {MaxBatchSize} withdrawals can be retried at once.");
        }

        _logger.LogInformation(
            "Admin {AdminUserId} is bulk-retrying {Count} withdrawal(s).",
            command.AdminUserId,
            withdrawalIds.Count);

        var items = new List<BulkWithdrawalItemResult>(withdrawalIds.Count);
        foreach (var withdrawalId in withdrawalIds)
        {
            try
            {
                var result = await _sender.Send(
                    new RetryWithdrawalCommand(command.AdminUserId, withdrawalId),
                    cancellationToken);
                items.Add(new BulkWithdrawalItemResult(withdrawalId, true, result.Status, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Admin bulk retry rejected withdrawal {WithdrawalId}.",
                    withdrawalId);
                items.Add(new BulkWithdrawalItemResult(
                    withdrawalId,
                    false,
                    null,
                    $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        return BulkSyncWithdrawalsCommandHandler.Summarize(items);
    }
}
