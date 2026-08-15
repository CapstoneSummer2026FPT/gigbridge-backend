using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Accounts.Services;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
using Application.Common.InternalServices.Elo.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Admin.Elo.Common;
using Application.Features.Elo.Common;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using Domain.Enums.Elo;
using Domain.Enums.Notifications;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Elo.Commands.ResolveEloAppeal;

public sealed class ResolveEloAppealCommandHandler : IRequestHandler<ResolveEloAppealCommand, EloAppealDto>
{
    private const int MaxNoteLength = 2000;
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly IAdminAuditService _audit;
    private readonly INotificationService _notifications;
    private readonly IUserEloService _elo;

    public ResolveEloAppealCommandHandler(
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

    public async Task<EloAppealDto> Handle(ResolveEloAppealCommand command, CancellationToken cancellationToken)
    {
        await AdminEloSupport.EnsureAdminAsync(_context, command.AdminId, cancellationToken);

        var appeal = await _context.Set<EloPointAppeal>()
            .FirstOrDefaultAsync(x => x.EloPointAppealId == command.AppealId, cancellationToken)
            ?? throw new NotFoundException("Elo appeal does not exist.");

        // Idempotent retry after a partial failure: the appeal is already resolved.
        if (IsResolved(appeal.Status))
            return EloAppealMappings.ToDto(appeal);
        if (appeal.Status == (int)EloPointAppealStatus.Cancelled)
            throw new BadRequestException("A cancelled appeal cannot be resolved.");

        var status = command.Status;
        if (!Enum.IsDefined(status) ||
            status is EloPointAppealStatus.Pending or EloPointAppealStatus.Cancelled)
            throw new BadRequestException("Invalid resolution status.");

        var resolution = command.Resolution;
        if (!Enum.IsDefined(resolution))
            throw new BadRequestException("Invalid appeal resolution.");

        var isFinal = status != EloPointAppealStatus.UnderReview;
        int? correctedDelta = command.CorrectedDelta;
        if (isFinal)
        {
            if (status == EloPointAppealStatus.Rejected)
            {
                resolution = EloPointAppealResolution.NoChange;
                correctedDelta = null;
            }
            else if (resolution == EloPointAppealResolution.NoChange)
            {
                throw new BadRequestException("An approved appeal must apply a correction.");
            }

            if (resolution is EloPointAppealResolution.PartialCorrection or EloPointAppealResolution.CustomAdjustment)
            {
                if (!correctedDelta.HasValue || correctedDelta.Value == 0)
                    throw new BadRequestException("A non-zero corrected delta is required for this resolution.");
            }
            else
            {
                correctedDelta = null;
            }
        }

        var note = command.ResolutionNote?.Trim();
        if (note?.Length > MaxNoteLength)
            throw new BadRequestException($"Resolution note must not exceed {MaxNoteLength} characters.");

        var before = new { appeal.Status, appeal.Resolution, appeal.CorrectedDelta };

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(AccountEnforcementLock.ForUser(appeal.UserId), cancellationToken);

        var created = isFinal
            ? await _elo.ApplyAppealResolutionAsync(
                appeal, resolution, correctedDelta, command.AdminId, cancellationToken)
            : null;

        var now = _clock.UtcNow;
        appeal.Status = (int)status;
        appeal.ReviewedByAdminId = command.AdminId;
        if (isFinal)
        {
            appeal.Resolution = (int)resolution;
            appeal.ResolutionNote = note;
            appeal.CorrectedDelta = correctedDelta;
            appeal.AppliedTransactionId = created?.UserEloPointTransactionsId;
            appeal.ReviewedAt = now;
        }
        appeal.UpdatedAt = now;

        _audit.Add(command.AdminId, isFinal ? "Elo.AppealResolution" : "Elo.AppealUnderReview",
            nameof(EloPointAppeal), appeal.EloPointAppealId, before,
            new { appeal.Status, appeal.Resolution, appeal.CorrectedDelta, appliedDelta = created?.PointsDelta });

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        try
        {
            var content = isFinal
                ? $"Your Elo appeal was {status.ToString()}.{FormatNote(note)}"
                : "Your Elo appeal is now under review.";
            await _notifications.CreateNotificationAsync(
                appeal.UserId,
                NotificationType.EloAppealStatusChanged,
                "Your Elo appeal status has changed",
                content,
                appeal.EloPointAppealId,
                nameof(EloPointAppeal),
                cancellationToken);
        }
        catch
        {
            // Notification must never fail the resolution.
        }

        return EloAppealMappings.ToDto(appeal);
    }

    private static bool IsResolved(int status)
    {
        return status == (int)EloPointAppealStatus.Approved ||
               status == (int)EloPointAppealStatus.PartiallyApproved ||
               status == (int)EloPointAppealStatus.Rejected;
    }

    private static string? FormatNote(string? note)
    {
        return string.IsNullOrWhiteSpace(note) ? null : $" {note}";
    }
}
