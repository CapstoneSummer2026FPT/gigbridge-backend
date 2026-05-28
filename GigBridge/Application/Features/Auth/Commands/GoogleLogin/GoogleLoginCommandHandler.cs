using Application.Common.Interfaces.IService;
using Application.Features.Auth.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Auth.Commands.GoogleLogin
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
            return await _authService.GoogleLoginWithRefreshAsync(request.AuthCode, cancellationToken);
        }
    }
}
