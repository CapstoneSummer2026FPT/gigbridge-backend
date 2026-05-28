using System.Threading;
using System.Threading.Tasks;
using Application.Features.Auth.DTOs;

namespace Application.Common.Interfaces.IService;

public interface IEmailService
{
    Task SendEmailAsync(EmailRequest emailRequestDTO, CancellationToken cancellationToken = default);

    Task VerifyEmailAsync(VerifyEmailRequest verifyEmailRequestDTO, CancellationToken cancellationToken = default);
}