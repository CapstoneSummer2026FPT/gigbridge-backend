using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using MediatR;

namespace Application.Features.Auth.VerifyOtp.Commands;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, Unit>
{
    private readonly ICacheService _cacheService;

    public VerifyOtpCommandHandler(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task<Unit> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var email = request.VerifyOtpRequest.Email.Trim().ToLowerInvariant();
        var otpKey = $"otp:{email}";
        var cachedOtp = await _cacheService.GetAsync<string>(otpKey, cancellationToken);

        if (string.IsNullOrEmpty(cachedOtp) || cachedOtp != request.VerifyOtpRequest.Otp)
        {
            throw new BadRequestException("Invalid or expired OTP verification code.");
        }

        await _cacheService.RemoveAsync(otpKey, cancellationToken);
        await _cacheService.SetAsync($"verified_email:{email}", request.VerifyOtpRequest.Otp, TimeSpan.FromMinutes(5), cancellationToken);

        return Unit.Value;
    }
}
