using Application.Common.Interfaces;
namespace Infrastructure.Services.Auth;
public class AuthService : IAuthService {
    private readonly IJwtService _jwtService;
    public AuthService(IJwtService jwtService) {
        _jwtService = jwtService;
    }
    public Task<string?> LoginAsync(string username, string password) {
        if (username == "admin@gigbridge.com" && password == "Admin123!") {
            return Task.FromResult<string?>(_jwtService.GenerateToken(username));
        }
        return Task.FromResult<string?>(null);
    }
}