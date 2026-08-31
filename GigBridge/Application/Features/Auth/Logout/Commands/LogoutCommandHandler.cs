using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Accounts.Services;
using Application.Common.InternalServices.Auth.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Logout.Commands;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IDateTimeService _dateTimeService;

    public LogoutCommandHandler(
        IApplicationDbContext context,
        IJwtService jwtService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _jwtService = jwtService;
        _dateTimeService = dateTimeService;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHashes = request.RefreshTokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .Select(_jwtService.HashRefreshToken)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (tokenHashes.Length == 0)
        {
            return;
        }

        var candidateUserIds = await _context.Set<User>()
            .Where(user =>
                (user.RefreshTokenHash != null && tokenHashes.Contains(user.RefreshTokenHash)) ||
                (user.PreviousRefreshTokenHash != null &&
                 tokenHashes.Contains(user.PreviousRefreshTokenHash) &&
                 user.PreviousRefreshTokenGraceExpiresAt >= _dateTimeService.UtcNow))
            .Select(user => user.UserId)
            .ToListAsync(cancellationToken);

        foreach (var userId in candidateUserIds.Distinct())
        {
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await transaction.AcquireTransactionLockAsync(
                AccountEnforcementLock.ForUser(userId),
                cancellationToken,
                "Auth.Logout.Revoke");

            var user = await _context.Set<User>()
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

            var stillMatches = user is not null &&
                ((user.RefreshTokenHash is not null && tokenHashes.Contains(user.RefreshTokenHash)) ||
                 (user.PreviousRefreshTokenHash is not null &&
                  tokenHashes.Contains(user.PreviousRefreshTokenHash) &&
                  user.PreviousRefreshTokenGraceExpiresAt >= _dateTimeService.UtcNow));

            if (!stillMatches)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            user!.RefreshTokenHash = null;
            user.RefreshTokenExpiry = null;
            user.PreviousRefreshTokenHash = null;
            user.PreviousRefreshTokenGraceExpiresAt = null;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
