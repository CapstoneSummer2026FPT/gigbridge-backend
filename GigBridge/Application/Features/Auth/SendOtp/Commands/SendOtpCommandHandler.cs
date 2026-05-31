using Application.Common.Interfaces.IService;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Auth.SendOtp.Commands
{
    public class SendOtpCommandHandler : IRequestHandler<SendOtpCommand, Unit>
    {
        private readonly IAuthService _authService;

        public SendOtpCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<Unit> Handle(SendOtpCommand request, CancellationToken cancellationToken)
        {
            await _authService.SendOtpAsync(request.SendOtpRequest, cancellationToken);
            return Unit.Value;
        }
    }
}
