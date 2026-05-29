namespace Application.Features.Admin.Users.CreateNewUser.DTOs;

public class CreateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Role { get; set; }
    public string? PhoneNumber { get; set; }
}
