using Application.Common.Interfaces;
namespace Infrastructure.Services.Auth;
public class AuthService : IAuthService {
    private readonly IJwtService _jwtService;
    public AuthService(IJwtService jwtService) {
        _jwtService = jwtService;
    }
    public async Task<string?> LoginAsync(string username, string password) {
        if (username == "admin@gigbridge.com" && password == "Admin123!") {
            return _jwtService.GenerateToken(username);
        }
        return null;
    }
}