using Application.Common.Interfaces.IService;
using Application.Features.Auth.DTOs;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Auth.Commands.Login;

public record LoginWithRefreshCommand(LoginRequest LoginRequest) : IRequest<(LoginResponse LoginData, string RefreshToken)>;

public class LoginWithRefreshCommandHandler : IRequestHandler<LoginWithRefreshCommand, (LoginResponse LoginData, string RefreshToken)>
{
    private readonly IAuthService _authService;

    public LoginWithRefreshCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<(LoginResponse LoginData, string RefreshToken)> Handle(LoginWithRefreshCommand request, CancellationToken cancellationToken)
    {
        return await _authService.LoginWithRefreshAsync(request.LoginRequest, cancellationToken);
    }
}
