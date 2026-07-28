namespace Application.Features.Auth.Shared.DTOs;

public class LoginResponse
{
    public UserDTO User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
}
