using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
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
        var email = resetRequest.Email.Trim().ToLowerInvariant();
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Email does not exist");
        }

        await EnsureOtpIsValidAsync(email, resetRequest.Otp, cancellationToken);

        user.Password = _passwordHasher.HashPassword(resetRequest.NewPassword);
        user.EmailVerificationToken = null;
        user.TokenExpiry = null;

        await _context.SaveChangesAsync(cancellationToken);
        
        // Remove verification status from cache so it cannot be reused
        await _cacheService.RemoveAsync($"verified_email:{email}", cancellationToken);
    }

    private async Task EnsureOtpIsValidAsync(string email, string otp, CancellationToken cancellationToken)
    {
        var cachedOtp = await _cacheService.GetAsync<string>($"verified_email:{email}", cancellationToken);

        if (string.IsNullOrEmpty(cachedOtp) || cachedOtp != otp)
        {
            throw new BadRequestException("Invalid or expired OTP verification code.");
        }
    }
}
