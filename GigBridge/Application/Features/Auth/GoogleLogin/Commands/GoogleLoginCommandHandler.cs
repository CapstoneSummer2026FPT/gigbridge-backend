using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Auth.GoogleLogin.Commands
{
    public class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, (LoginResponse LoginData, string RefreshToken)>
    {
        private readonly IAuthService _authService;
        public GoogleLoginCommandHandler(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task<(LoginResponse LoginData, string RefreshToken)> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
        {
            return await _authService.GoogleLoginWithRefreshAsync(request.AuthCode, request.Role, cancellationToken);
        }
    }
}
