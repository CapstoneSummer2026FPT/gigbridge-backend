using Application.Common.Interfaces.IService;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Auth.ForgotPassword.Commands
{
    public class SendEmailPasswordChangingCommandHandler : IRequestHandler<SendEmailPasswordChangingCommand>
    {
        private readonly IAuthService _authService;

        public SendEmailPasswordChangingCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task Handle(SendEmailPasswordChangingCommand request, CancellationToken cancellationToken)
        {
            await _authService.SendEmailPasswordChangingRequestAsync(request.Request, cancellationToken);
        }
    }
}
