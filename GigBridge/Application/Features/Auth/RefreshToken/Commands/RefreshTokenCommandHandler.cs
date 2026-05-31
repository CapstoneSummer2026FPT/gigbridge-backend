using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Application.Features.Auth.RefreshToken.Commands;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, (LoginResponse LoginData, string RefreshToken)>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMapper _mapper;

    public RefreshTokenCommandHandler(
        IApplicationDbContext context,
        IJwtService jwtService,
        IDateTimeService dateTimeService,
        IMapper mapper)
    {
        _context = context;
        _jwtService = jwtService;
        _dateTimeService = dateTimeService;
        _mapper = mapper;
    }

    public async Task<(LoginResponse LoginData, string RefreshToken)> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromAccessToken(request.AccessToken);
        var user = await LoadUserAsync(userId, cancellationToken);

        EnsureRefreshTokenIsValid(user, request.RefreshToken);

        var newRefreshToken = RotateRefreshToken(user);
        await _context.SaveChangesAsync(cancellationToken);

        return (new LoginResponse
        {
            User = _mapper.Map<UserDTO>(user),
            Token = _jwtService.GenerateToken(user)
        }, newRefreshToken);
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
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Your account has been suspended by the administrator");
        }

        return user;
    }

    private void EnsureRefreshTokenIsValid(User user, string refreshToken)
    {
        var incomingHash = _jwtService.HashRefreshToken(refreshToken);

        if (user.RefreshTokenHash != incomingHash)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        if (user.RefreshTokenExpiry < _dateTimeService.UtcNow)
        {
            throw new UnauthorizedAccessException("Refresh token expired");
        }
    }

    private string RotateRefreshToken(User user)
    {
        var refreshToken = _jwtService.GenerateRefreshToken();
        user.RefreshTokenHash = _jwtService.HashRefreshToken(refreshToken);
        user.RefreshTokenExpiry = _dateTimeService.UtcNow.AddDays(7);
        return refreshToken;
    }
}
