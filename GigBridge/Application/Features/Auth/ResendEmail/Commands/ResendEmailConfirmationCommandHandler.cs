using Application.Common.Interfaces.IService;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Auth.ResendEmail.Commands
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
