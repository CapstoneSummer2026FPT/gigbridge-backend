using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Elo.Common;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Elo.Commands.CreateEloAppeal;

public sealed class CreateEloAppealCommandHandler : IRequestHandler<CreateEloAppealCommand, EloAppealDto>
{
    private const int MaxReasonLength = 2000;
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly IMediaService _media;

    public CreateEloAppealCommandHandler(
        IApplicationDbContext context,
        IDateTimeService clock,
        IMediaService media)
    {
        _context = context;
        _clock = clock;
        _media = media;
    }

    public async Task<EloAppealDto> Handle(CreateEloAppealCommand command, CancellationToken cancellationToken)
    {
        var reason = command.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new BadRequestException("Appeal reason is required.");
        if (reason.Length > MaxReasonLength)
            throw new BadRequestException($"Appeal reason must not exceed {MaxReasonLength} characters.");
        EloAppealEvidenceSupport.ValidateOptionalBatch(command.Files);

        var transaction = await _context.Set<UserEloPointTransaction>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserEloPointTransactionsId == command.TransactionId, cancellationToken)
            ?? throw new NotFoundException("Elo transaction does not exist.");
        if (transaction.UserId != command.UserId)
            throw new ForbiddenAccessException("You cannot appeal another user's Elo change.");
        if (transaction.Reason == (int)UserEloPointReason.InitialGrant)
            throw new BadRequestException("The initial point grant cannot be appealed.");

        // Idempotency: at most one active appeal per transaction (also enforced by
        // the filtered unique index). A retry after a partial failure re-posts the
        // existing appeal rather than creating a duplicate.
        var existing = await _context.Set<EloPointAppeal>()
            .AsNoTracking()
            .Where(x => x.EloPointTransactionId == command.TransactionId &&
                        (x.Status == (int)EloPointAppealStatus.Pending ||
                         x.Status == (int)EloPointAppealStatus.UnderReview))
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return EloAppealMappings.ToDto(existing);

        var now = _clock.UtcNow;
        var appeal = new EloPointAppeal
        {
            EloPointAppealId = Guid.NewGuid(),
            UserId = command.UserId,
            EloPointTransactionId = command.TransactionId,
            Status = (int)EloPointAppealStatus.Pending,
            Reason = reason,
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.Set<EloPointAppeal>().Add(appeal);

        var uploaded = new List<EloPointAppealEvidence>();
        foreach (var file in command.Files)
        {
            uploaded.Add(await EloAppealEvidenceSupport.UploadAsync(
                _media, file, appeal.EloPointAppealId, command.UserId, now, cancellationToken));
        }
        if (uploaded.Count > 0)
            _context.Set<EloPointAppealEvidence>().AddRange(uploaded);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Concurrent duplicate submission tripped the active-appeal unique index.
            throw new ConflictException("An appeal for this transaction has already been submitted.", exception);
        }

        return EloAppealMappings.ToDto(appeal);
    }
}
