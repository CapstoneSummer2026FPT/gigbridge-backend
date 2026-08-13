using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Elo.Common;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using Domain.Enums.Elo;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Elo.Commands.CancelEloAppeal;

public sealed class CancelEloAppealCommandHandler : IRequestHandler<CancelEloAppealCommand, EloAppealDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;

    public CancelEloAppealCommandHandler(IApplicationDbContext context, IDateTimeService clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<EloAppealDto> Handle(CancelEloAppealCommand command, CancellationToken cancellationToken)
    {
        var appeal = await _context.Set<EloPointAppeal>()
            .FirstOrDefaultAsync(x => x.EloPointAppealId == command.AppealId, cancellationToken)
            ?? throw new NotFoundException("Elo appeal does not exist.");
        if (appeal.UserId != command.UserId)
            throw new ForbiddenAccessException("You cannot cancel another user's appeal.");
        if (appeal.Status != (int)EloPointAppealStatus.Pending)
            throw new ConflictException("Only a pending appeal can be cancelled.");

        var now = _clock.UtcNow;
        appeal.Status = (int)EloPointAppealStatus.Cancelled;
        appeal.CancelledById = command.UserId;
        appeal.CancelledAt = now;
        appeal.UpdatedAt = now;

        await _context.SaveChangesAsync(cancellationToken);
        return EloAppealMappings.ToDto(appeal);
    }
}
