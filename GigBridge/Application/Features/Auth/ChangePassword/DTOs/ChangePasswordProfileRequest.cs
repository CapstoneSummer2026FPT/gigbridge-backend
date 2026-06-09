namespace Application.Features.Auth.ChangePassword.DTOs;

public class ChangePasswordProfileRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
