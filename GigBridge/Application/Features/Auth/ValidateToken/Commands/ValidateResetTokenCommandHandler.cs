using Application.Common.Interfaces.IService;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Auth.ValidateToken.Commands
{
    public class ValidateResetTokenCommandHandler : IRequestHandler<ValidateResetTokenCommand, bool>
    {
        private readonly IAuthService _authService;

        public ValidateResetTokenCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<bool> Handle(ValidateResetTokenCommand request, CancellationToken cancellationToken)
        {
            return await _authService.IsTokenExpired(request.Request.Token, cancellationToken);
        }
    }
}
