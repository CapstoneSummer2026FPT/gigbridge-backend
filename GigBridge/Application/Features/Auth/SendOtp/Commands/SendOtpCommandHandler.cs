using Application.Common.Exceptions;
using Application.Common.Interfaces.Caching;
using Application.Features.Auth.Common.Interfaces;
using Application.Features.Auth.Common;
using MediatR;
using System.Security.Cryptography;

namespace Application.Features.Auth.SendOtp.Commands;

public class SendOtpCommandHandler : IRequestHandler<SendOtpCommand, Unit>
{
    private readonly ICacheService _cacheService;
    private readonly IAuthEmailSender _authEmailSender;

    public SendOtpCommandHandler(ICacheService cacheService, IAuthEmailSender authEmailSender)
    {
        _cacheService = cacheService;
        _authEmailSender = authEmailSender;
    }

    public async Task<Unit> Handle(SendOtpCommand request, CancellationToken cancellationToken)
    {
        var email = EmailCanonicalizer.Canonicalize(request.SendOtpRequest.Email);
        if (!OtpPurposeNames.TryParse(request.SendOtpRequest.Purpose, out var purpose)
            || purpose != OtpPurpose.Signup)
        {
            throw new BadRequestException("Invalid verification purpose.");
        }

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
        await _cacheService.SetAsync(
            OtpSecurity.CooldownKey(purpose, email),
            true,
            OtpSecurity.ResendCooldown,
            cancellationToken);
        await _authEmailSender.SendOtpEmailAsync(email, otp, cancellationToken);

        return Unit.Value;
    }
}
