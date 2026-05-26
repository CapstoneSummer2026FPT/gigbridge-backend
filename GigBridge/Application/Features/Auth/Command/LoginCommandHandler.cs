using Application.Common.Interfaces;
using Application.Features.Auth.Dto;
using MediatR;
namespace Application.Features.Auth.Command;
public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse?> {
    private readonly IAuthService _authService;
    public LoginCommandHandler(IAuthService authService) {
        _authService = authService;
    }
    public async Task<LoginResponse?> Handle(LoginCommand request, CancellationToken cancellationToken) {
        var token = await _authService.LoginAsync(request.Username, request.Password);
        if (token == null) return null;
        return new LoginResponse { Token = token };
    }
}
