namespace Application.Common.Interfaces.IService;

public interface IAuthEmailSender
{
    Task SendVerificationEmailAsync(string email, string token, CancellationToken cancellationToken = default);

    Task SendPasswordResetEmailAsync(string email, string token, CancellationToken cancellationToken = default);
}
