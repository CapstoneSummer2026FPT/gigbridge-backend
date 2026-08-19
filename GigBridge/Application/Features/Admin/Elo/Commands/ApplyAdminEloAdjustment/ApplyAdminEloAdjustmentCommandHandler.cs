using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Accounts.Services;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
using Application.Common.InternalServices.Elo.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Admin.Elo.Common;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Elo;
using Domain.Enums.Notifications;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Elo.Commands.ApplyAdminEloAdjustment;

public sealed class ApplyAdminEloAdjustmentCommandHandler :
    IRequestHandler<ApplyAdminEloAdjustmentCommand, EloTransactionDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly IAdminAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly IUserEloService _elo;

    public ApplyAdminEloAdjustmentCommandHandler(
        IApplicationDbContext context,
        IDateTimeService clock,
        IAdminAuditService audit,
        INotificationService notifications,
        IUserEloService elo)
    {
        _context = context;
        _clock = clock;
        _audit = audit;
        _notifications = notifications;
        _elo = elo;
    }

    public async Task<EloTransactionDto?> Handle(
        ApplyAdminEloAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        await AdminEloSupport.EnsureAdminAsync(_context, command.AdminId, cancellationToken);
        ValidateAmount(command.Mode, command.Amount);

        var user = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("User does not exist.");
        if (user.Role is not ((int)UserRole.Client) and not ((int)UserRole.Freelancer))
            throw new BadRequestException("Elo adjustments can only be applied to client or freelancer accounts.");

        var currentPoints = await _context.Set<UserEloScore>()
            .AsNoTracking()
            .Where(x => x.UserId == command.UserId)
            .Select(x => (int?)x.CurrentPoints)
            .FirstOrDefaultAsync(cancellationToken)
            ?? UserEloCalculator.DefaultPoints;

        var delta = CalculateDelta(currentPoints, command.Mode, command.Amount, command.Increase);
        if (delta == 0)
            throw new BadRequestException(command.Increase
                ? "The computed increase is zero. Increase the percentage or use fixed points."
                : "The user has no points left to deduct.");

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(AccountEnforcementLock.ForUser(command.UserId), cancellationToken);

        var created = await _elo.ApplyAdminAdjustmentAsync(
            command.AdminId,
            command.UserId,
            delta,
            command.Reason?.Trim(),
            command.RequestId,
            cancellationToken);

        EloTransactionDto? result;
        if (created is not null)
        {
            // Record how the delta was computed (FixedPoints/Percentage) so the
            // ledger row is self-describing, mirroring the dispute-penalty rows.
            created.Mode = (int)command.Mode;
            result = new EloTransactionDto(
                created.UserEloPointTransactionsId, created.UserId, created.PointsDelta,
                created.PointsBefore, created.PointsAfter, created.Reason, created.SourceType,
                created.Mode, created.SourceEntityType, created.SourceEntityId, created.ContractId,
                created.ReviewId, created.Rating, created.EloAppealId, created.AppliedByAdminId,
                created.CreatedAt);
        }
        else
        {
            // Idempotent retry: load the transaction written by the first attempt.
            result = await _context.Set<UserEloPointTransaction>()
                .AsNoTracking()
                .Where(x => x.UserId == command.UserId &&
                            x.IdempotencyKey == $"elo-admin:{command.RequestId}")
                .Select(x => new EloTransactionDto(
                    x.UserEloPointTransactionsId, x.UserId, x.PointsDelta, x.PointsBefore,
                    x.PointsAfter, x.Reason, x.SourceType, x.Mode, x.SourceEntityType,
                    x.SourceEntityId, x.ContractId, x.ReviewId, x.Rating, x.EloAppealId,
                    x.AppliedByAdminId, x.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken);
        }

        _audit.Add(command.AdminId, "Elo.AdminAdjustment", nameof(UserEloPointTransaction),
            result?.TransactionId,
            new { command.UserId, pointsBefore = currentPoints },
            new
            {
                command.UserId, command.Increase, command.Mode, command.Amount, command.Reason,
                command.RequestId, delta, pointsAfter = result?.PointsAfter
            });

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (result is not null)
        {
            try
            {
                await _notifications.CreateNotificationAsync(
                    command.UserId,
                    NotificationType.EloPointsUpdated,
                    "Your Elo points have been adjusted",
                    command.Reason?.Trim(),
                    result.TransactionId,
                    nameof(UserEloPointTransaction),
                    cancellationToken);
            }
            catch
            {
                // Notification must never fail the adjustment.
            }
        }

        return result;
    }

    private static void ValidateAmount(EloAdjustmentMode mode, decimal amount)
    {
        if (!Enum.IsDefined(mode))
            throw new BadRequestException("Invalid adjustment mode.");

        if (mode == EloAdjustmentMode.Percentage && (amount <= 0 || amount > 100))
            throw new BadRequestException("Percentage adjustment must be between 1 and 100.");

        if (mode == EloAdjustmentMode.FixedPoints && (amount <= 0 || amount != decimal.Truncate(amount)))
            throw new BadRequestException("Fixed points adjustment must be a positive whole number.");
    }

    private static int CalculateDelta(int currentPoints, EloAdjustmentMode mode, decimal amount, bool increase)
    {
        int requested;
        if (mode == EloAdjustmentMode.Percentage)
        {
            requested = increase
                ? (int)Math.Round(currentPoints * amount / 100m, MidpointRounding.AwayFromZero)
                : UserEloCalculator.CalculatePenaltyDelta(currentPoints, EloAdjustmentMode.Percentage, amount);
        }
        else
        {
            var fixedDelta = Math.Abs((int)Math.Round(amount, MidpointRounding.AwayFromZero));
            requested = increase ? fixedDelta : -fixedDelta;
        }

        // Return the effective delta after the score's ≥0 clamp so that a decrease
        // from zero points yields 0 (rejected by the caller) instead of a phantom
        // zero-delta ledger row.
        return UserEloCalculator.ApplyDelta(currentPoints, requested) - currentPoints;
    }
}
