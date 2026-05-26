using Application.Common.Interfaces.IService;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Auth.Commands.ResendEmail
{
    public class ResendEmailConfirmationCommandHandler : IRequestHandler<ResendEmailConfirmationCommand>
    {
        private readonly IAuthService _authService;

        public ResendEmailConfirmationCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task Handle(ResendEmailConfirmationCommand request, CancellationToken cancellationToken)
        {
            await _authService.ResendEmailConfirmationAsync(request.Request, cancellationToken);
        }
    }
}
