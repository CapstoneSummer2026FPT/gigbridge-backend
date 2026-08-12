using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Caching;
using Application.Features.Auth.Common.Interfaces;
using Application.Features.Auth.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Application.Features.Auth.ForgotPassword.Commands;

public class SendEmailPasswordChangingCommandHandler : IRequestHandler<SendEmailPasswordChangingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly IAuthEmailSender _authEmailSender;

    public SendEmailPasswordChangingCommandHandler(
        IApplicationDbContext context,
        ICacheService cacheService,
        IAuthEmailSender authEmailSender)
    {
        _context = context;
        _cacheService = cacheService;
        _authEmailSender = authEmailSender;
    }

    public async Task Handle(SendEmailPasswordChangingCommand request, CancellationToken cancellationToken)
    {
        var email = EmailCanonicalizer.Canonicalize(request.Request.Email);
        const OtpPurpose purpose = OtpPurpose.PasswordReset;

        if (await _cacheService.GetAsync<bool>(
                OtpSecurity.LockoutKey(purpose, email),
                cancellationToken))
        {
            throw new BadRequestException("Too many failed verification attempts. Please try again later.");
        }

        if (await _cacheService.GetAsync<bool>(
                OtpSecurity.CooldownKey(purpose, email),
                cancellationToken))
        {
            throw new BadRequestException("Please wait before requesting another verification code.");
        }

        await _cacheService.SetAsync(
            OtpSecurity.CooldownKey(purpose, email),
            true,
            OtpSecurity.ResendCooldown,
            cancellationToken);

        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            return;
        }

        var challengeKey = OtpSecurity.ChallengeKey(purpose, email);
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var challenge = new OtpChallengeState(
            otp,
            0,
            DateTime.UtcNow.Add(OtpSecurity.ChallengeLifetime));

        await _cacheService.RemoveAsync(challengeKey, cancellationToken);
        await _cacheService.SetAsync(
            challengeKey,
            challenge,
            OtpSecurity.ChallengeLifetime,
            cancellationToken);
        await _authEmailSender.SendForgotPasswordOtpEmailAsync(user.Email, otp, cancellationToken);
    }
}
