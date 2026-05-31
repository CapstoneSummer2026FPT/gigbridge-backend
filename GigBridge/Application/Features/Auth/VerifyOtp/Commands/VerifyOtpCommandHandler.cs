using Application.Common.Interfaces.IService;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Auth.VerifyOtp.Commands
{
    public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, Unit>
    {
        private readonly IAuthService _authService;

        public VerifyOtpCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<Unit> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
        {
            await _authService.VerifyOtpAsync(request.VerifyOtpRequest, cancellationToken);
            return Unit.Value;
        }
    }
}
