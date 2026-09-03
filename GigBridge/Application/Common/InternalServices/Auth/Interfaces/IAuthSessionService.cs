using Application.Common.InternalServices.Auth.Models;
using Domain.Entities;

namespace Application.Common.InternalServices.Auth.Interfaces;

public interface IAuthSessionService
{
    Task<IssuedRefreshToken> CreateLoginSessionAsync(
        User user,
        CancellationToken cancellationToken);

    Task<IssuedRefreshToken> RotateRefreshSessionAsync(
        User user,
        IReadOnlyCollection<string> refreshTokens,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> FindSessionOwnerIdsAsync(
        IReadOnlyCollection<string> refreshTokens,
        CancellationToken cancellationToken);

    Task<bool> RevokeMatchingSessionsAsync(
        User user,
        IReadOnlyCollection<string> refreshTokens,
        CancellationToken cancellationToken);
}
