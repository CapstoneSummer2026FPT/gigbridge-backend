using Application.Common.Interfaces.IService;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Auth.VerifyEmail.Commands
{
    public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand>
    {
        private readonly IEmailService _emailService;

        public VerifyEmailCommandHandler(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public async Task Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
        {
            await _emailService.VerifyEmailAsync(request.VerifyEmailRequest, cancellationToken);
        }
    }
}
