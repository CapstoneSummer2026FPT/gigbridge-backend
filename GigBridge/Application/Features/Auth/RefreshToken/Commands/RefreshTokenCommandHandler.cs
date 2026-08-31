using Application.Common.Interfaces;
using Application.Common.InternalServices.Accounts.Services;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Auth.Interfaces;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Application.Features.Auth.RefreshToken.Commands;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, (LoginResponse LoginData, string RefreshToken, DateTime RefreshTokenExpiry)>
{
    /// <summary>
    /// How long a just-superseded refresh token stays acceptable after rotation. Absorbs
    /// legitimate concurrent refresh attempts (e.g. two browser tabs of the same session
    /// racing on the same httpOnly cookie) without weakening rotation/revocation for a
    /// genuinely stale or reused token, which is rejected once this window elapses.
    /// </summary>
    private static readonly TimeSpan RefreshTokenGracePeriod = TimeSpan.FromSeconds(30);

    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMapper _mapper;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IApplicationDbContext context,
        IJwtService jwtService,
        IDateTimeService dateTimeService,
        IMapper mapper,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _dateTimeService = dateTimeService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<(LoginResponse LoginData, string RefreshToken, DateTime RefreshTokenExpiry)> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromAccessToken(request.AccessToken);
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            AccountEnforcementLock.ForUser(userId),
            cancellationToken,
            "Auth.RefreshToken.Rotate");

        // Reload only after taking the per-user lock. Otherwise two refresh requests can
        // validate the same old token, both return success, and immediately invalidate
        // one of the newly issued tokens when the second SaveChanges wins.
        var user = await LoadUserAsync(userId, cancellationToken);

        EnsureRefreshTokenIsValid(user, GetRefreshTokenCandidates(request));

        var newRefreshToken = RotateRefreshToken(user);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Refresh token rotated for user {UserId} (role {Role}).",
            user.UserId,
            user.Role);

        return (new LoginResponse
        {
            User = _mapper.Map<UserDTO>(user),
            Token = _jwtService.GenerateToken(user)
        }, newRefreshToken, user.RefreshTokenExpiry ?? DateTime.UtcNow);
    }

    private Guid GetUserIdFromAccessToken(string accessToken)
    {
        var principal = _jwtService.GetPrincipalFromExpiredToken(accessToken);
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            throw new UnauthorizedAccessException("Invalid access token");
        }

        return parsedUserId;
    }

    private async Task<User> LoadUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .Include(u => u.ClientProfile)
            .Include(u => u.FreelancerProfile)
            .Include(u => u.UserEloScore)
            .Include(u => u.Subscriptions)
                .ThenInclude(subscription => subscription.SubscriptionPlans)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        UserAccountEnforcement.EnsureCanAuthenticate(user, _dateTimeService.UtcNow);

        return user;
    }

    private IReadOnlyCollection<string> GetRefreshTokenCandidates(RefreshTokenCommand request)
    {
        return (request.RefreshTokenCandidates ?? [request.RefreshToken])
            .Append(request.RefreshToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private void EnsureRefreshTokenIsValid(User user, IReadOnlyCollection<string> refreshTokens)
    {
        var incomingHashes = refreshTokens
            .Select(_jwtService.HashRefreshToken)
            .ToHashSet(StringComparer.Ordinal);
        var now = _dateTimeService.UtcNow;

        var matchesCurrent =
            user.RefreshTokenHash is not null &&
            incomingHashes.Contains(user.RefreshTokenHash);
        var matchesRecentPrevious =
            !matchesCurrent &&
            user.PreviousRefreshTokenHash is not null &&
            incomingHashes.Contains(user.PreviousRefreshTokenHash) &&
            user.PreviousRefreshTokenGraceExpiresAt is DateTime graceExpiresAt &&
            graceExpiresAt >= now;

        if (!matchesCurrent && !matchesRecentPrevious)
        {
            _logger.LogWarning(
                "Refresh rejected for user {UserId}: token does not match the current or recently-rotated token.",
                user.UserId);
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        if (matchesCurrent && user.RefreshTokenExpiry < now)
        {
            _logger.LogWarning("Refresh rejected for user {UserId}: current refresh token expired.", user.UserId);
            throw new UnauthorizedAccessException("Refresh token expired");
        }

        if (matchesRecentPrevious)
        {
            _logger.LogInformation(
                "Refresh accepted for user {UserId} via rotation grace window (concurrent refresh, e.g. a sibling browser tab).",
                user.UserId);
        }
    }

    private string RotateRefreshToken(User user)
    {
        var now = _dateTimeService.UtcNow;

        // Preserve the just-superseded token for a short grace window instead of discarding
        // it immediately, so a second legitimate concurrent refresh still succeeds.
        user.PreviousRefreshTokenHash = user.RefreshTokenHash;
        user.PreviousRefreshTokenGraceExpiresAt = now.Add(RefreshTokenGracePeriod);

        var refreshToken = _jwtService.GenerateRefreshToken();
        user.RefreshTokenHash = _jwtService.HashRefreshToken(refreshToken);
        user.RefreshTokenExpiry = now.AddMinutes(_jwtService.GetRefreshTokenExpiryMinutes());
        return refreshToken;
    }
}
