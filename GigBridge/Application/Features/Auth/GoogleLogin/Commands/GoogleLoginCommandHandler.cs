using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.GoogleLogin.DTOs;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.GoogleLogin.Commands;

public class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, (LoginResponse LoginData, string RefreshToken)>
{
    private readonly IApplicationDbContext _context;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IJwtService _jwtService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMapper _mapper;

    public GoogleLoginCommandHandler(
        IApplicationDbContext context,
        IGoogleAuthService googleAuthService,
        IJwtService jwtService,
        IDateTimeService dateTimeService,
        IMapper mapper)
    {
        _context = context;
        _googleAuthService = googleAuthService;
        _jwtService = jwtService;
        _dateTimeService = dateTimeService;
        _mapper = mapper;
    }

    public async Task<(LoginResponse LoginData, string RefreshToken)> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        var googleUser = await _googleAuthService.VerifyAuthCodeAsync(request.AuthCode, cancellationToken);
        var user = await FindUserAsync(googleUser.Email, cancellationToken) ?? CreateFreelancerUser(googleUser);

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Your account has been suspended by the administrator");
        }

        var refreshToken = RotateRefreshToken(user);
        await _context.SaveChangesAsync(cancellationToken);

        return (new LoginResponse
        {
            User = _mapper.Map<UserDTO>(user),
            Token = _jwtService.GenerateToken(user),
            refreshToken = refreshToken
        }, refreshToken);
    }

    private Task<User?> FindUserAsync(string email, CancellationToken cancellationToken)
    {
        return _context.Set<User>()
            .Include(u => u.ClientProfile)
            .Include(u => u.FreelancerProfile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    private User CreateFreelancerUser(GoogleUserInfoDTO googleUser)
    {
        var now = _dateTimeService.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = googleUser.Email,
            FullName = googleUser.Name,
            Role = 1,
            Provider = "Google",
            ProviderId = googleUser.GoogleId,
            IsEmailVerified = true,
            IsActive = true,
            CreatedAt = now,
            Avatar = googleUser.PictureUrl,
            FreelancerProfile = new FreelancerProfile
            {
                FreelancerProfilesId = Guid.NewGuid(),
                CreatedAt = now
            }
        };

        _context.Set<User>().Add(user);
        return user;
    }

    private string RotateRefreshToken(User user)
    {
        var refreshToken = _jwtService.GenerateRefreshToken();
        user.RefreshTokenHash = _jwtService.HashRefreshToken(refreshToken);
        user.RefreshTokenExpiry = _dateTimeService.UtcNow.AddDays(7);
        return refreshToken;
    }
}
