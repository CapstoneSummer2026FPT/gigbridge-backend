using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Common;
using Application.Features.Auth.VerifyOtp.DTOs;
using MediatR;
using System.Security.Cryptography;

namespace Application.Features.Auth.VerifyOtp.Commands;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, VerifyOtpResponse>
{
    private readonly ICacheService _cacheService;

    public VerifyOtpCommandHandler(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task<VerifyOtpResponse> Handle(
        VerifyOtpCommand request,
        CancellationToken cancellationToken)
    {
        var email = EmailCanonicalizer.Canonicalize(request.VerifyOtpRequest.Email);
        if (!OtpPurposeNames.TryParse(request.VerifyOtpRequest.Purpose, out var purpose))
        {
            throw new BadRequestException("Invalid verification purpose.");
        }

        var lockoutKey = OtpSecurity.LockoutKey(purpose, email);
        if (await _cacheService.GetAsync<bool>(lockoutKey, cancellationToken))
        {
            throw new BadRequestException("Too many failed verification attempts. Please try again later.");
        }

        var challengeKey = OtpSecurity.ChallengeKey(purpose, email);
        var challenge = await _cacheService.GetAndRemoveAsync<OtpChallengeState>(
            challengeKey,
            cancellationToken);

        if (challenge is null || challenge.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new BadRequestException("Invalid or expired OTP verification code.");
        }

        if (!OtpSecurity.Matches(challenge.Otp, request.VerifyOtpRequest.Otp))
        {
            var failedAttempts = challenge.FailedAttempts + 1;
            if (failedAttempts >= OtpSecurity.MaxFailedAttempts)
            {
                await _cacheService.SetAsync(
                    lockoutKey,
                    true,
                    OtpSecurity.LockoutDuration,
                    cancellationToken);
            }
            else
            {
                var remainingLifetime = challenge.ExpiresAtUtc - DateTime.UtcNow;
                await _cacheService.SetAsync(
                    challengeKey,
                    challenge with { FailedAttempts = failedAttempts },
                    remainingLifetime > TimeSpan.Zero ? remainingLifetime : TimeSpan.FromSeconds(1),
                    cancellationToken);
            }

            throw new BadRequestException("Invalid or expired OTP verification code.");
        }

        var verificationProof = purpose == OtpPurpose.Signup
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()
            : request.VerifyOtpRequest.Otp;

        await _cacheService.SetAsync(
            OtpSecurity.VerifiedKey(purpose, email, verificationProof),
            true,
            OtpSecurity.ChallengeLifetime,
            cancellationToken);

        return new VerifyOtpResponse
        {
            VerificationTicket = purpose == OtpPurpose.Signup
                ? verificationProof
                : null
        };
    }
}
