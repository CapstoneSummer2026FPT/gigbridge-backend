using Application.Common.Interfaces.IService;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Auth.ResetPassword.Commands
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
