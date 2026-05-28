using System.Threading;
using System.Threading.Tasks;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Auth.VerifyEmail.DTOs;

namespace Application.Common.Interfaces.IService;

public interface IEmailService
{
    Task SendEmailAsync(EmailRequest emailRequestDTO, CancellationToken cancellationToken = default);

    Task VerifyEmailAsync(VerifyEmailRequest verifyEmailRequestDTO, CancellationToken cancellationToken = default);
}