namespace Application.Common.InternalServices.Auth.Interfaces;
public interface IAuthEmailSender
{
    Task SendOtpEmailAsync(string email, string otp, CancellationToken cancellationToken = default);

    Task SendIdentityVerificationOtpEmailAsync(
        string email,
        string otp,
        CancellationToken cancellationToken = default);

    Task SendForgotPasswordOtpEmailAsync(string email, string otp, CancellationToken cancellationToken = default);
}
