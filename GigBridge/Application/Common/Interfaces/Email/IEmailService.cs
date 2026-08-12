using System.Threading;
using System.Threading.Tasks;
using Application.Features.Auth.Shared.DTOs;

namespace Application.Common.Interfaces.Email;

public interface IEmailService
{
    Task SendEmailAsync(EmailRequest emailRequestDTO, CancellationToken cancellationToken = default);
}
