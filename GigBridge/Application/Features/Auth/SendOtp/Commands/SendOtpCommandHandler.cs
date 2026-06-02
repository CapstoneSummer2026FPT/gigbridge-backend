using Application.Common.Interfaces.IService;
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
        var email = request.SendOtpRequest.Email.Trim().ToLowerInvariant();
        var cacheKey = $"otp:{email}";
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        await _cacheService.RemoveAsync(cacheKey, cancellationToken);
        await _cacheService.SetAsync(cacheKey, otp, TimeSpan.FromMinutes(1), cancellationToken);
        await _authEmailSender.SendOtpEmailAsync(email, otp, cancellationToken);

        return Unit.Value;
    }
}
