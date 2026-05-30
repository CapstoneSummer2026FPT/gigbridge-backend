using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Microsoft.EntityFrameworkCore;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Auth.Login.DTOs;
using Application.Features.Auth.Register.DTOs;
using Application.Features.Auth.ResendEmail.DTOs;
using Application.Features.Auth.ForgotPassword.DTOs;
using Application.Features.Auth.ResetPassword.DTOs;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
namespace Infrastructure.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IJwtService _jwtService;
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly IGoogleAuthService _googleAuthService;

    public AuthService(
        IJwtService jwtService,
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IMapper mapper,
        IEmailService emailService,
        IWebHostEnvironment webHostEnvironment,
        IConfiguration configuration,
        IGoogleAuthService googleAuthService)
    {
        _jwtService = jwtService;
        _context = context;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
        _emailService = emailService;
        _webHostEnvironment = webHostEnvironment;
        _configuration = configuration;
        _googleAuthService = googleAuthService;
    }

    public async Task<UserDTO> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Set<User>().FirstOrDefaultAsync(c => c.Email.ToLower() == request.Email.ToLower(), cancellationToken: cancellationToken);
        if (existing != null)
        {
            throw new Exception("Email already exists");
        }

        var hash = _passwordHasher.HashPassword(request.Password);
        var token = Guid.NewGuid().ToString();

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = request.Email,
            FullName = request.FullName ?? request.Email,
            Password = hash,
            Role = request.role,
            IsEmailVerified = false, // true for testing
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            EmailVerificationToken = token,
            TokenExpiry = DateTime.UtcNow.AddHours(24)
        };

        if (user.Role == 0) // Client
        {
            user.ClientProfile = new ClientProfile
            {
                ClientProfilesId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
        }
        else if (user.Role == 1) // Freelancer
        {
            user.FreelancerProfile = new FreelancerProfile
            {
                FreelancerProfilesId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
        }

        _context.Set<User>().Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        var frontendUrl = _configuration["FrontendBaseUrl"] ?? "https://localhost:7094";
        var verifyLink = $"{frontendUrl}/api/Auth/verify-email?token={token}";

        var path = Path.Combine(
            _webHostEnvironment.ContentRootPath,
            "Templates",
            "VerifyEmail.html"
        );

        var body = await File.ReadAllTextAsync(path, cancellationToken);
        body = body.Replace("{{VERIFICATION_URL}}", verifyLink);

        await _emailService.SendEmailAsync(new EmailRequest
        {
            Body = body,
            To = user.Email,
            Subject = "Welcome to GigBridge! Please Confirm Your Email",
            IsHtml = true,
            Attachments = null
        }, cancellationToken);

        return _mapper.Map<UserDTO>(user);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _context.Set<User>()
                .Include(u => u.ClientProfile)
                .Include(u => u.FreelancerProfile)
                .FirstOrDefaultAsync(c => c.Email == request.Email, cancellationToken: cancellationToken);

            if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.Password))
            {
                return null;
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Your account has been suspended by the administrator");
            }

            if (!user.IsEmailVerified)
            {
                throw new UnauthorizedAccessException("Account has not been verified");
            }

            return new LoginResponse
            {
                User = _mapper.Map<UserDTO>(user),
                Token = _jwtService.GenerateToken(user)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during login: {ex.Message}");
            throw;
        }
    }

    public async Task<(LoginResponse loginData, string refreshToken)> LoginWithRefreshAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Set<User>()
            .Include(u => u.ClientProfile)
            .Include(u => u.FreelancerProfile)
            .FirstOrDefaultAsync(c => c.Email == request.Email, cancellationToken: cancellationToken);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.Password))
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Your account has been suspended by the administrator");
        }

        if (!user.IsEmailVerified)
        {
            throw new UnauthorizedAccessException("Account has not been verified");
        }

        var accessToken = _jwtService.GenerateToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshTokenHash = _jwtService.HashRefreshToken(refreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync(cancellationToken);

        return (new LoginResponse
        {
            User = _mapper.Map<UserDTO>(user),
            Token = accessToken,
            refreshToken = refreshToken
        }, refreshToken);
    }

    public async Task<(LoginResponse loginData, string refreshToken)> GoogleLoginWithRefreshAsync(string authCode, CancellationToken cancellationToken = default)
    {
        var googleUser = await _googleAuthService.VerifyAuthCodeAsync(authCode, cancellationToken);
        var user = await _context.Set<User>()
            .Include(u => u.ClientProfile)
            .Include(u => u.FreelancerProfile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == googleUser.Email.ToLower(), cancellationToken: cancellationToken);

        if (user == null)
        {
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = googleUser.Email,
                FullName = googleUser.Name,
                Role = 1, // Default to Freelancer
                Provider = "Google",
                ProviderId = googleUser.GoogleId,
                IsEmailVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Avatar = googleUser.PictureUrl
            };

            user.FreelancerProfile = new FreelancerProfile
            {
                FreelancerProfilesId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Set<User>().Add(user);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Your account has been suspended by the administrator");
        }

        var accessToken = _jwtService.GenerateToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshTokenHash = _jwtService.HashRefreshToken(refreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync(cancellationToken);

        return (new LoginResponse
        {
            User = _mapper.Map<UserDTO>(user),
            Token = accessToken
        }, refreshToken);
    }

    public async Task<(LoginResponse loginData, string refreshToken)> RefreshTokenAsync(string expiredAccessToken, string refreshToken, CancellationToken cancellationToken = default)
    {
        var principal = _jwtService.GetPrincipalFromExpiredToken(expiredAccessToken);
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("Invalid access token");
        }

        var user = await _context.Set<User>()
            .Include(u => u.ClientProfile)
            .Include(u => u.FreelancerProfile)
            .FirstOrDefaultAsync(u => u.UserId == Guid.Parse(userId), cancellationToken: cancellationToken);

        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Your account has been suspended by the administrator");
        }

        var incomingHash = _jwtService.HashRefreshToken(refreshToken);
        if (user.RefreshTokenHash != incomingHash)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        if (user.RefreshTokenExpiry < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Refresh token expired");
        }

        var newAccessToken = _jwtService.GenerateToken(user);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        user.RefreshTokenHash = _jwtService.HashRefreshToken(newRefreshToken);
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync(cancellationToken);

        return (new LoginResponse
        {
            User = _mapper.Map<UserDTO>(user),
            Token = newAccessToken
        }, newRefreshToken);
    }

    public async Task ResendEmailConfirmationAsync(EmailResendConfirmationRequest email, CancellationToken cancellationToken = default)
    {
        var user = await _context.Set<User>().FirstOrDefaultAsync(c => c.Email.ToLower() == email.Email.ToLower(), cancellationToken: cancellationToken);

        if (user == null)
        {
            throw new Exception("Email does not exist");
        }

        if (user.IsEmailVerified)
        {
            throw new Exception("Account has already been verified");
        }

        var token = Guid.NewGuid().ToString();

        user.EmailVerificationToken = token;
        user.TokenExpiry = DateTime.UtcNow.AddHours(24);

        await _context.SaveChangesAsync(cancellationToken);

        var frontendUrl = _configuration["FrontendBaseUrl"] ?? "https://localhost:7094";
        var verifyLink = $"{frontendUrl}/api/Auth/verify-email?token={token}";

        var path = Path.Combine(
            _webHostEnvironment.ContentRootPath,
            "Templates",
            "VerifyEmail.html"
        );

        var body = await File.ReadAllTextAsync(path, cancellationToken);
        body = body.Replace("{{VERIFICATION_URL}}", verifyLink);

        await _emailService.SendEmailAsync(new EmailRequest
        {
            Body = body,
            To = user.Email,
            Subject = "GigBridge: Please Confirm Your Email",
            IsHtml = true,
            Attachments = null
        }, cancellationToken);
    }

    public async Task SendEmailPasswordChangingRequestAsync(ForgotPasswordRequest email, CancellationToken cancellationToken = default)
    {
        var user = await _context.Set<User>().FirstOrDefaultAsync(c => c.Email.ToLower() == email.Email.ToLower(), cancellationToken: cancellationToken);

        if (user == null)
        {
            throw new Exception("Email does not exist");
        }



        var token = Guid.NewGuid().ToString();

        user.EmailVerificationToken = token;
        user.TokenExpiry = DateTime.UtcNow.AddHours(1);

        await _context.SaveChangesAsync(cancellationToken);

        var frontendUrl = _configuration["FrontendBaseUrl"] ?? "https://localhost:7094";
        var verifyLink = $"{frontendUrl}/api/Auth/reset-password?token={token}";

        var path = Path.Combine(
            _webHostEnvironment.ContentRootPath,
            "Templates",
            "ResetPassword.html"
        );

        var body = await File.ReadAllTextAsync(path, cancellationToken);
        body = body.Replace("{{RESET_URL}}", verifyLink);

        await _emailService.SendEmailAsync(new EmailRequest
        {
            Body = body,
            To = user.Email,
            Subject = "GigBridge: Please Confirm Your New Password",
            IsHtml = true,
            Attachments = null
        }, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Set<User>().FirstOrDefaultAsync(c => c.Email.ToLower() == request.Email.ToLower(), cancellationToken: cancellationToken);


        if (user == null)
        {
            throw new Exception("Email does not exist");
        }


        if (request.PasswordResetToken != user.EmailVerificationToken)
        {
            throw new Exception("wrong email verification token");
        }

        if (user.TokenExpiry < DateTime.UtcNow)
        {
            throw new Exception("Token has expired");
        }


        var hash = _passwordHasher.HashPassword(request.NewPassword);
        user.Password = hash;
        user.EmailVerificationToken = null;
        user.TokenExpiry = null;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsTokenExpired(string token, CancellationToken cancellationToken = default)
    {
        var user = await _context.Set<User>().FirstOrDefaultAsync(c => c.EmailVerificationToken == token, cancellationToken: cancellationToken);

        if (user != null)
        {
            return user.TokenExpiry < DateTime.UtcNow;
        }

        return true;
    }
}
