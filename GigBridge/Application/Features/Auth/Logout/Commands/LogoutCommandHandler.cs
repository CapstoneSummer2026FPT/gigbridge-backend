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
    private readonly IAuthSessionService _authSessionService;

    public LogoutCommandHandler(
        IApplicationDbContext context,
        IAuthSessionService authSessionService)
    {
        _context = context;
        _authSessionService = authSessionService;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (request.RefreshTokens.Count == 0)
        {
            return;
        }

        var candidateUserIds = await _authSessionService.FindSessionOwnerIdsAsync(
            request.RefreshTokens,
            cancellationToken);

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
                await _authSessionService.RevokeMatchingSessionsAsync(
                    user,
                    request.RefreshTokens,
                    cancellationToken);

            if (!stillMatches)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
