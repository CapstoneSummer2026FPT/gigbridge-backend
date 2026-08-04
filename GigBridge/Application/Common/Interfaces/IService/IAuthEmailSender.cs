namespace Application.Common.Interfaces.IService;

public interface IAuthEmailSender
{
    Task SendOtpEmailAsync(string email, string otp, CancellationToken cancellationToken = default);

    Task SendForgotPasswordOtpEmailAsync(string email, string otp, CancellationToken cancellationToken = default);
}
