using Application.Common.Exceptions;
using Application.Common.Interfaces.Caching;
using Application.Common.InternalServices.Auth.Interfaces;
using Application.Common.InternalServices.Auth.Services;
using Application.Common.Interfaces.Identity;
using MediatR;
using System.Security.Cryptography;

namespace Application.Features.Auth.SendOtp.Commands;

public class SendOtpCommandHandler : IRequestHandler<SendOtpCommand, Unit>
{
    private readonly ICacheService _cacheService;
    private readonly IAuthEmailSender _authEmailSender;
    private readonly ICurrentUserService _currentUserService;

    public SendOtpCommandHandler(
        ICacheService cacheService,
        IAuthEmailSender authEmailSender,
        ICurrentUserService currentUserService)
    {
        _cacheService = cacheService;
        _authEmailSender = authEmailSender;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(SendOtpCommand request, CancellationToken cancellationToken)
    {
        var email = EmailCanonicalizer.Canonicalize(request.SendOtpRequest.Email);
        if (!OtpPurposeNames.TryParse(request.SendOtpRequest.Purpose, out var purpose)
            || purpose == OtpPurpose.PasswordReset)
        {
            throw new BadRequestException("Invalid verification purpose.");
        }

        EnsureIdentityVerificationBelongsToCurrentUser(purpose, email);

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
        if (purpose == OtpPurpose.IdentityVerification)
        {
            await _authEmailSender.SendIdentityVerificationOtpEmailAsync(
                email,
                otp,
                cancellationToken);
        }
        else
        {
            await _authEmailSender.SendOtpEmailAsync(email, otp, cancellationToken);
        }

        return Unit.Value;
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
}
