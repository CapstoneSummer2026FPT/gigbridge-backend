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
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IAuthSessionService _authSessionService;
    private readonly IMapper _mapper;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IApplicationDbContext context,
        IJwtService jwtService,
        IDateTimeService dateTimeService,
        IAuthSessionService authSessionService,
        IMapper mapper,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _dateTimeService = dateTimeService;
        _authSessionService = authSessionService;
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

        var newRefreshToken = await _authSessionService.RotateRefreshSessionAsync(
            user,
            GetRefreshTokenCandidates(request),
            cancellationToken);
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
        }, newRefreshToken.Token, newRefreshToken.ExpiresAt);
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

}
