using Application.Common.Interfaces.IService;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Auth.Commands.ResetPassword
{
    public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
    {
        private readonly IAuthService _authService;

        public ResetPasswordCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        {
            await _authService.ResetPasswordAsync(request.Request, cancellationToken);
        }
    }
}
