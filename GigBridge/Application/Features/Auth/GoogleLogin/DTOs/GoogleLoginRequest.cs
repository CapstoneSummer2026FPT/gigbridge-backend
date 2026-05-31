namespace Application.Features.Auth.GoogleLogin.DTOs;

public class GoogleLoginRequest
{
    public string AuthCode { get; set; } = null!;

    public int? Role { get; set; }
}
