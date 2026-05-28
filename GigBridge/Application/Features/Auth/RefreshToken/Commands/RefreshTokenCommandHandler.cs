using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Auth.RefreshToken.Commands
{
    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, (LoginResponse LoginData, string RefreshToken)>
    {
        private readonly IAuthService _authService;

        public RefreshTokenCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<(LoginResponse LoginData, string RefreshToken)> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            return await _authService.RefreshTokenAsync(request.AccessToken, request.RefreshToken, cancellationToken);
        }
    }
}
