using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Wallets.Common.DTOs;
using Application.Features.Wallets.Common.Withdrawals;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

public sealed class AdminMarkWithdrawalFailedCommandHandler :
    IRequestHandler<AdminMarkWithdrawalFailedCommand, WithdrawalResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public AdminMarkWithdrawalFailedCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<WithdrawalResponse> Handle(
        AdminMarkWithdrawalFailedCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Request.Reason))
        {
            throw new BadRequestException("Manual failure reason is required.");
        }

        var withdrawal = await _context.Set<WalletWithdrawal>()
            .FirstOrDefaultAsync(
                withdrawal => withdrawal.WalletWithdrawalId == command.WithdrawalId,
                cancellationToken);

        if (withdrawal is null)
        {
            throw new NotFoundException("Withdrawal does not exist.");
        }

        if (withdrawal.Status != (int)Domain.Enums.WithdrawalStatus.SyncRequired)
        {
            throw new ConflictException("Only SYNC_REQUIRED withdrawals can be manually marked as failed.");
        }

        var now = _dateTimeService.UtcNow;
        await WithdrawalWorkflow.FinalizeFailedAsync(
            _context,
            _dateTimeService,
            withdrawal,
            command.Request.Reason.Trim(),
            null,
            cancellationToken);

        _context.Set<AdminAuditLog>().Add(new AdminAuditLog
        {
            AdminAuditLogsId = Guid.NewGuid(),
            AdminId = command.AdminUserId,
            Action = "WithdrawalMarkFailed",
            EntityId = withdrawal.WalletWithdrawalId,
            EntityType = nameof(WalletWithdrawal),
            NewValues = command.Request.Reason.Trim(),
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);
        return WithdrawalResponse.FromEntity(withdrawal);
    }
}
