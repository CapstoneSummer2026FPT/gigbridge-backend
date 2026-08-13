using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Media;
using Application.Common.Interfaces.Time;
using Application.Features.Elo.Common;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using Domain.Enums.Elo;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Elo.Commands.UploadEloAppealEvidence;

public sealed class UploadEloAppealEvidenceCommandHandler :
    IRequestHandler<UploadEloAppealEvidenceCommand, IReadOnlyList<EloAppealEvidenceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly IMediaService _media;

    public UploadEloAppealEvidenceCommandHandler(
        IApplicationDbContext context,
        IDateTimeService clock,
        IMediaService media)
    {
        _context = context;
        _clock = clock;
        _media = media;
    }

    public async Task<IReadOnlyList<EloAppealEvidenceDto>> Handle(
        UploadEloAppealEvidenceCommand command,
        CancellationToken cancellationToken)
    {
        EloAppealEvidenceSupport.ValidateOptionalBatch(command.Files);

        var appeal = await _context.Set<EloPointAppeal>()
            .FirstOrDefaultAsync(x => x.EloPointAppealId == command.AppealId, cancellationToken)
            ?? throw new NotFoundException("Elo appeal does not exist.");
        if (appeal.UserId != command.UserId)
            throw new ForbiddenAccessException("You cannot modify another user's appeal.");
        if (appeal.Status != (int)EloPointAppealStatus.Pending)
            throw new ConflictException("Evidence can only be added to a pending appeal.");

        var now = _clock.UtcNow;
        var evidence = new List<EloPointAppealEvidence>();
        foreach (var file in command.Files)
        {
            evidence.Add(await EloAppealEvidenceSupport.UploadAsync(
                _media, file, appeal.EloPointAppealId, command.UserId, now, cancellationToken));
        }

        if (evidence.Count > 0)
        {
            _context.Set<EloPointAppealEvidence>().AddRange(evidence);
            appeal.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return evidence.Select(x => new EloAppealEvidenceDto(
            x.EloPointAppealEvidenceId, x.EloPointAppealId, x.UploadedById, x.FileName,
            x.FileUrl, x.FileSize, x.Description, x.CreatedAt)).ToList();
    }
}
