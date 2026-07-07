using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Wallets.Common.DTOs;
using Application.Features.Wallets.Common.Withdrawals;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

public sealed class RetryWithdrawalCommandHandler :
    IRequestHandler<RetryWithdrawalCommand, WithdrawalResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public RetryWithdrawalCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<WithdrawalResponse> Handle(
        RetryWithdrawalCommand command,
        CancellationToken cancellationToken)
    {
        var withdrawal = await _context.Set<WalletWithdrawal>()
            .FirstOrDefaultAsync(
                withdrawal => withdrawal.WalletWithdrawalId == command.WithdrawalId,
                cancellationToken);

        if (withdrawal is null)
        {
            throw new NotFoundException("Withdrawal does not exist.");
        }

        if (WithdrawalWorkflow.IsTerminal(withdrawal.Status))
        {
            throw new ConflictException("Terminal withdrawal cannot be retried.");
        }

        var now = _dateTimeService.UtcNow;
        var outbox = await _context.Set<PayoutOutbox>()
            .FirstOrDefaultAsync(
                outbox => outbox.WalletWithdrawalId == withdrawal.WalletWithdrawalId,
                cancellationToken);

        if (outbox is null)
        {
            outbox = new PayoutOutbox
            {
                PayoutOutboxId = Guid.NewGuid(),
                WalletWithdrawalId = withdrawal.WalletWithdrawalId,
                PayoutKey = $"withdrawal:{withdrawal.WalletWithdrawalId:D}:create",
                CreatedAt = now
            };
            _context.Set<PayoutOutbox>().Add(outbox);
        }

        outbox.Status = (int)PayoutOutboxStatus.Pending;
        outbox.NextAttemptAt = now;
        outbox.LastError = null;
        outbox.ProcessedAt = null;

        withdrawal.UpdatedAt = now;
        withdrawal.LastSyncError = null;

        AddAudit(command.AdminUserId, withdrawal.WalletWithdrawalId, "WithdrawalRetry", now);

        await _context.SaveChangesAsync(cancellationToken);
        return WithdrawalResponse.FromEntity(withdrawal);
    }

    private void AddAudit(Guid adminUserId, Guid withdrawalId, string action, DateTime now)
    {
        _context.Set<AdminAuditLog>().Add(new AdminAuditLog
        {
            AdminAuditLogsId = Guid.NewGuid(),
            AdminId = adminUserId,
            Action = action,
            EntityId = withdrawalId,
            EntityType = nameof(WalletWithdrawal),
            CreatedAt = now
        });
    }
}
