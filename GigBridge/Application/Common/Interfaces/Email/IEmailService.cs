using System.Threading;
using System.Threading.Tasks;
namespace Application.Common.Interfaces.Email;

public interface IEmailService
{
    Task SendEmailAsync(EmailRequest emailRequestDTO, CancellationToken cancellationToken = default);
}
