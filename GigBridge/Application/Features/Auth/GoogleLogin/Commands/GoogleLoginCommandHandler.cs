using Application.Common.InternalServices.Auth.Models;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Accounts.Services;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Auth.Interfaces;
using Application.Common.InternalServices.Elo.Interfaces;
using Application.Common.InternalServices.Auth.Services;
using Application.Features.Auth.GoogleLogin.DTOs;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using Domain.Enums.Accounts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.GoogleLogin.Commands;

public class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, (LoginResponse LoginData, string RefreshToken, DateTime RefreshTokenExpiry)>
{
    private readonly IApplicationDbContext _context;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IJwtService _jwtService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IUserEloService _userEloService;
    private readonly IAuthSessionService _authSessionService;
    private readonly IMapper _mapper;

    public GoogleLoginCommandHandler(
        IApplicationDbContext context,
        IGoogleAuthService googleAuthService,
        IJwtService jwtService,
        IDateTimeService dateTimeService,
        IUserEloService userEloService,
        IAuthSessionService authSessionService,
        IMapper mapper)
    {
        _context = context;
        _googleAuthService = googleAuthService;
        _jwtService = jwtService;
        _dateTimeService = dateTimeService;
        _userEloService = userEloService;
        _authSessionService = authSessionService;
        _mapper = mapper;
    }

    public async Task<(LoginResponse LoginData, string RefreshToken, DateTime RefreshTokenExpiry)> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        var googleUser = await _googleAuthService.VerifyAuthCodeAsync(request.AuthCode, cancellationToken);
        var email = EmailCanonicalizer.Canonicalize(googleUser.Email);
        var user = await FindUserAsync(email, cancellationToken);
        var isNewUser = user is null;

        if (user is null)
        {
            if (request.IsFromSignIn == true)
            {
                throw new BadRequestException("Your account does not have a role set up yet. Please select a role on the sign-up page before signing in.");
            }

            user = CreateUser(googleUser, email, ResolveRole(request.Role));
            _context.Set<User>().Add(user);
            await _userEloService.InitializeNewUserAsync(user, cancellationToken);
        }

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            AccountEnforcementLock.ForUser(user.UserId),
            cancellationToken,
            "Auth.GoogleLogin.CreateSession");

        UserAccountEnforcement.EnsureCanAuthenticate(user, _dateTimeService.UtcNow);

        if (!isNewUser)
        {
            await _userEloService.ApplyLoginActivityAsync(user, cancellationToken);
        }

        var refreshToken = await _authSessionService.CreateLoginSessionAsync(
            user,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (new LoginResponse
        {
            User = _mapper.Map<UserDTO>(user),
            Token = _jwtService.GenerateToken(user)
        }, refreshToken.Token, refreshToken.ExpiresAt);
    }

    private Task<User?> FindUserAsync(string email, CancellationToken cancellationToken)
    {
        return _context.Set<User>()
            .Include(u => u.ClientProfile)
            .Include(u => u.FreelancerProfile)
            .Include(u => u.UserEloScore)
            .Include(u => u.Subscriptions)
                .ThenInclude(subscription => subscription.SubscriptionPlans)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    private User CreateUser(GoogleUserInfoDTO googleUser, string email, UserRole role)
    {
        var now = _dateTimeService.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            FullName = googleUser.Name,
            Role = (int)role,
            Provider = "Google",
            ProviderId = googleUser.GoogleId,
            Password = null,
            IsEmailVerified = true,
            IsActive = true,
            CreatedAt = now,
            Avatar = googleUser.PictureUrl
        };

        user.AttachProfileForRole(now);
        return user;
    }

    private static UserRole ResolveRole(int? role)
    {
        return role switch
        {
            null => UserRole.Freelancer,
            0 => UserRole.Client,
            1 => UserRole.Freelancer,
            _ => throw new BadRequestException("Invalid Google login role")
        };
    }

}
