using Application.Common.Interfaces;
using Application.Features.Auth.DTOs;
using MediatR;
namespace Application.Features.Auth.Commands.Login;
public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse?> {
    private readonly IAuthService _authService;
    public LoginCommandHandler(IAuthService authService) {
        _authService = authService;
    }
    public async Task<LoginResponse?> Handle(LoginCommand request, CancellationToken cancellationToken) {
        var token = await _authService.LoginAsync(request.LoginRequest.Username, request.LoginRequest.Password);
        if (token == null) return null;
        return new LoginResponse { Token = token };
    }
}