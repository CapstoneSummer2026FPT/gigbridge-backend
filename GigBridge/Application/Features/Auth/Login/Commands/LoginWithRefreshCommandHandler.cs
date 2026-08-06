using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Common;
using Application.Common.Services;
using Application.Features.Auth.Shared.DTOs;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Login.Commands
{
    public class LoginWithRefreshCommandHandler : IRequestHandler<LoginWithRefreshCommand, (LoginResponse LoginData, string RefreshToken, DateTime RefreshTokenExpiry)>
    {
        private readonly IApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtService _jwtService;
        private readonly IDateTimeService _dateTimeService;
        private readonly IUserEloService _userEloService;
        private readonly IMapper _mapper;

        public LoginWithRefreshCommandHandler(
            IApplicationDbContext context,
            IPasswordHasher passwordHasher,
            IJwtService jwtService,
            IDateTimeService dateTimeService,
            IUserEloService userEloService,
            IMapper mapper)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _jwtService = jwtService;
            _dateTimeService = dateTimeService;
            _userEloService = userEloService;
            _mapper = mapper;
        }

        public async Task<(LoginResponse LoginData, string RefreshToken, DateTime RefreshTokenExpiry)> Handle(LoginWithRefreshCommand request, CancellationToken cancellationToken)
        {
            var email = EmailCanonicalizer.Canonicalize(request.LoginRequest.Email);
            var user = await _context.Set<User>()
                .Include(u => u.ClientProfile)
                .Include(u => u.FreelancerProfile)
                .Include(u => u.UserEloScore)
                .Include(u => u.Subscriptions)
                    .ThenInclude(subscription => subscription.SubscriptionPlans)
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            if (user is null || string.IsNullOrEmpty(user.Password) || !_passwordHasher.VerifyPassword(request.LoginRequest.Password, user.Password))
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            EnsureUserCanLogin(user, _dateTimeService.UtcNow);

            await _userEloService.ApplyLoginActivityAsync(user, cancellationToken);
            var refreshToken = RotateRefreshToken(user);
            await _context.SaveChangesAsync(cancellationToken);

            return (new LoginResponse
            {
                User = _mapper.Map<UserDTO>(user),
                Token = _jwtService.GenerateToken(user)
            }, refreshToken, user.RefreshTokenExpiry ?? DateTime.UtcNow);
        }

        private string RotateRefreshToken(User user)
        {
            var refreshToken = _jwtService.GenerateRefreshToken();
            user.RefreshTokenHash = _jwtService.HashRefreshToken(refreshToken);
            user.RefreshTokenExpiry = _dateTimeService.UtcNow.AddMinutes(_jwtService.GetRefreshTokenExpiryMinutes());
            return refreshToken;
        }

        private static void EnsureUserCanLogin(User user, DateTime now)
        {
            UserAccountEnforcement.EnsureCanAuthenticate(user, now);

            if (!user.IsEmailVerified)
            {
                throw new UnauthorizedAccessException("Account has not been verified");
            }
        }
    }
}
