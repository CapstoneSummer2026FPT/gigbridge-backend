using Application.Common.Exceptions;
using Application.Common.Interfaces.Caching;
using Application.Common.InternalServices.Auth.Services;
using Application.Features.Auth.VerifyOtp.DTOs;
using Application.Common.Interfaces.Identity;
using MediatR;
using System.Security.Cryptography;

namespace Application.Features.Auth.VerifyOtp.Commands;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, VerifyOtpResponse>
{
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;

    public VerifyOtpCommandHandler(
        ICacheService cacheService,
        ICurrentUserService currentUserService)
    {
        _cacheService = cacheService;
        _currentUserService = currentUserService;
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

        EnsureIdentityVerificationBelongsToCurrentUser(purpose, email);

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

        var verificationProof = purpose is OtpPurpose.Signup or OtpPurpose.IdentityVerification
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()
            : request.VerifyOtpRequest.Otp;

        var verificationContext = purpose == OtpPurpose.IdentityVerification
            ? NormalizeIdentityCode(request.VerifyOtpRequest.IdentityOrTaxCode)
            : null;

        await _cacheService.SetAsync(
            OtpSecurity.VerifiedKey(purpose, email, verificationProof, verificationContext),
            true,
            OtpSecurity.ChallengeLifetime,
            cancellationToken);

        return new VerifyOtpResponse
        {
            VerificationTicket = purpose is OtpPurpose.Signup or OtpPurpose.IdentityVerification
                ? verificationProof
                : null
        };
    }

    private void EnsureIdentityVerificationBelongsToCurrentUser(OtpPurpose purpose, string email)
    {
        if (purpose != OtpPurpose.IdentityVerification)
        {
            return;
        }

        var authenticatedEmail = _currentUserService.Email;
        if (string.IsNullOrWhiteSpace(authenticatedEmail) ||
            !string.Equals(
                EmailCanonicalizer.Canonicalize(authenticatedEmail),
                email,
                StringComparison.Ordinal))
        {
            throw new ForbiddenAccessException(
                "Identity verification must use the authenticated account email.");
        }
    }

    private static string? NormalizeIdentityCode(string? value) =>
        value is null
            ? null
            : string.Concat(value.Where(character => !char.IsWhiteSpace(character)));
}
