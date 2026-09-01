using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Auth.Interfaces;
using Application.Common.InternalServices.Auth.Models;
using Application.Common.Options;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Common.InternalServices.Auth.Services;

public sealed class AuthSessionService : IAuthSessionService
{
    private static readonly TimeSpan RefreshTokenGracePeriod = TimeSpan.FromSeconds(30);

    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IDateTimeService _dateTimeService;
    private readonly AuthSessionOptions _options;
    private readonly ILogger<AuthSessionService> _logger;

    public AuthSessionService(
        IApplicationDbContext context,
        IJwtService jwtService,
        IDateTimeService dateTimeService,
        IOptions<AuthSessionOptions> options,
        ILogger<AuthSessionService> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _dateTimeService = dateTimeService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IssuedRefreshToken> CreateLoginSessionAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var now = _dateTimeService.UtcNow;
        var issued = GenerateRefreshToken(now);

        var sessions = await _context.Set<AuthSession>()
            .Where(session => session.UserId == user.UserId)
            .ToListAsync(cancellationToken);

        var expiredSessions = sessions
            .Where(session => session.RefreshTokenExpiry <= now)
            .ToArray();
        _context.Set<AuthSession>().RemoveRange(expiredSessions);

        var activeSessions = sessions
            .Except(expiredSessions)
            .OrderBy(session => session.LastUsedAt)
            .ThenBy(session => session.CreatedAt)
            .ToArray();
        var sessionsToRevoke = Math.Max(
            0,
            activeSessions.Length - _options.MaxActiveSessionsPerUser + 1);

        if (sessionsToRevoke > 0)
        {
            _context.Set<AuthSession>().RemoveRange(activeSessions.Take(sessionsToRevoke));
            _logger.LogInformation(
                "Revoked {SessionCount} oldest auth session(s) for user {UserId} while enforcing the active-session limit.",
                sessionsToRevoke,
                user.UserId);
        }

        _context.Set<AuthSession>().Add(new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            RefreshTokenHash = _jwtService.HashRefreshToken(issued.Token),
            RefreshTokenExpiry = issued.ExpiresAt,
            CreatedAt = now,
            LastUsedAt = now
        });

        MirrorLegacySession(user, issued, previousHash: null, previousGraceExpiresAt: null);
        return issued;
    }

    public async Task<IssuedRefreshToken> RotateRefreshSessionAsync(
        User user,
        IReadOnlyCollection<string> refreshTokens,
        CancellationToken cancellationToken)
    {
        var orderedIncomingHashes = HashTokensInOrder(refreshTokens);
        var incomingHashes = orderedIncomingHashes.ToHashSet(StringComparer.Ordinal);
        var now = _dateTimeService.UtcNow;

        var candidates = await _context.Set<AuthSession>()
            .Where(session =>
                session.UserId == user.UserId &&
                (incomingHashes.Contains(session.RefreshTokenHash) ||
                 (session.PreviousRefreshTokenHash != null &&
                  incomingHashes.Contains(session.PreviousRefreshTokenHash))))
            .ToListAsync(cancellationToken);

        var session = orderedIncomingHashes
                .Select(hash => candidates.FirstOrDefault(candidate =>
                    candidate.RefreshTokenHash == hash))
                .FirstOrDefault(candidate => candidate is not null)
            ?? orderedIncomingHashes
                .Select(hash => candidates.FirstOrDefault(candidate =>
                    candidate.PreviousRefreshTokenHash == hash &&
                    candidate.PreviousRefreshTokenGraceExpiresAt >= now))
                .FirstOrDefault(candidate => candidate is not null);

        if (session is null)
        {
            _logger.LogWarning(
                "Refresh rejected for user {UserId}: token does not match an active auth session.",
                user.UserId);
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        if (session.RefreshTokenExpiry <= now)
        {
            _logger.LogWarning(
                "Refresh rejected for user {UserId}: auth session {SessionId} expired.",
                user.UserId,
                session.Id);
            throw new UnauthorizedAccessException("Refresh token expired");
        }

        var previousHash = session.RefreshTokenHash;
        var issued = GenerateRefreshToken(now);
        session.PreviousRefreshTokenHash = previousHash;
        session.PreviousRefreshTokenGraceExpiresAt = now.Add(RefreshTokenGracePeriod);
        session.RefreshTokenHash = _jwtService.HashRefreshToken(issued.Token);
        session.RefreshTokenExpiry = issued.ExpiresAt;
        session.LastUsedAt = now;

        MirrorLegacySession(
            user,
            issued,
            previousHash,
            session.PreviousRefreshTokenGraceExpiresAt);
        return issued;
    }

    public async Task<IReadOnlyList<Guid>> FindSessionOwnerIdsAsync(
        IReadOnlyCollection<string> refreshTokens,
        CancellationToken cancellationToken)
    {
        var tokenHashes = HashTokens(refreshTokens);
        if (tokenHashes.Count == 0)
        {
            return [];
        }

        var now = _dateTimeService.UtcNow;
        return await _context.Set<AuthSession>()
            .Where(session =>
                tokenHashes.Contains(session.RefreshTokenHash) ||
                (session.PreviousRefreshTokenHash != null &&
                 tokenHashes.Contains(session.PreviousRefreshTokenHash) &&
                 session.PreviousRefreshTokenGraceExpiresAt >= now))
            .Select(session => session.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeMatchingSessionsAsync(
        User user,
        IReadOnlyCollection<string> refreshTokens,
        CancellationToken cancellationToken)
    {
        var tokenHashes = HashTokens(refreshTokens);
        var now = _dateTimeService.UtcNow;
        var changed = false;

        var sessions = await _context.Set<AuthSession>()
            .Where(session =>
                session.UserId == user.UserId &&
                (tokenHashes.Contains(session.RefreshTokenHash) ||
                 (session.PreviousRefreshTokenHash != null &&
                  tokenHashes.Contains(session.PreviousRefreshTokenHash) &&
                  session.PreviousRefreshTokenGraceExpiresAt >= now)))
            .ToListAsync(cancellationToken);

        if (sessions.Count > 0)
        {
            _context.Set<AuthSession>().RemoveRange(sessions);
            changed = true;
        }

        if (LegacySessionMatches(user, tokenHashes, now))
        {
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiry = null;
            user.PreviousRefreshTokenHash = null;
            user.PreviousRefreshTokenGraceExpiresAt = null;
            changed = true;
        }

        return changed;
    }

    private IssuedRefreshToken GenerateRefreshToken(DateTime now)
    {
        var token = _jwtService.GenerateRefreshToken();
        var expiresAt = now.AddMinutes(_jwtService.GetRefreshTokenExpiryMinutes());
        return new IssuedRefreshToken(token, expiresAt);
    }

    private HashSet<string> HashTokens(IEnumerable<string> refreshTokens)
    {
        return HashTokensInOrder(refreshTokens).ToHashSet(StringComparer.Ordinal);
    }

    private IReadOnlyList<string> HashTokensInOrder(IEnumerable<string> refreshTokens)
    {
        return refreshTokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .Select(_jwtService.HashRefreshToken)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool LegacySessionMatches(
        User user,
        IReadOnlySet<string> incomingHashes,
        DateTime now)
    {
        return
            (user.RefreshTokenHash is not null && incomingHashes.Contains(user.RefreshTokenHash)) ||
            (user.PreviousRefreshTokenHash is not null &&
             incomingHashes.Contains(user.PreviousRefreshTokenHash) &&
             user.PreviousRefreshTokenGraceExpiresAt >= now);
    }

    private void MirrorLegacySession(
        User user,
        IssuedRefreshToken issued,
        string? previousHash,
        DateTime? previousGraceExpiresAt)
    {
        user.RefreshTokenHash = _jwtService.HashRefreshToken(issued.Token);
        user.RefreshTokenExpiry = issued.ExpiresAt;
        user.PreviousRefreshTokenHash = previousHash;
        user.PreviousRefreshTokenGraceExpiresAt = previousGraceExpiresAt;
    }
}
