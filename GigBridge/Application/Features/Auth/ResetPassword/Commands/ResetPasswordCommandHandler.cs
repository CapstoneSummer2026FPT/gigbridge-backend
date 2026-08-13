using Application.Common.Interfaces;
using Application.Common.Interfaces.Caching;
using Application.Features.Auth.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.ResetPassword.Commands;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICacheService _cacheService;

    public ResetPasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ICacheService cacheService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _cacheService = cacheService;
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var resetRequest = request.Request;
        var email = EmailCanonicalizer.Canonicalize(resetRequest.Email);
        var verificationKey = OtpSecurity.VerifiedKey(
            OtpPurpose.PasswordReset,
            email,
            resetRequest.Otp);
        var isVerified = await _cacheService.GetAndRemoveAsync<bool>(
            verificationKey,
            cancellationToken);

        if (!isVerified)
        {
            throw new Application.Common.Exceptions.BadRequestException(
                "Invalid or expired OTP verification code.");
        }

        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            throw new Application.Common.Exceptions.BadRequestException(
                "Invalid or expired OTP verification code.");
        }

        user.Password = _passwordHasher.HashPassword(resetRequest.NewPassword);
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiry = null;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
