namespace Application.Features.Admin.Users.SuspendUser.DTOs;

public class SuspendUserRequest
{
    public string Email { get; set; } = string.Empty;
    public DateTime SuspendedUntil { get; set; }
    public string? Reason { get; set; }
}
