using Application.Common.Interfaces.IService;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Auth.Commands.VerifyEmail
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
