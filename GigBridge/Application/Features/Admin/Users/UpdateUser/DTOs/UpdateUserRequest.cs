namespace Application.Features.Admin.Users.UpdateUser.DTOs;

public class UpdateUserRequest
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Avatar { get; set; }
    public string? PreferredLanguage { get; set; }
    public bool? IsActive { get; set; }
}
