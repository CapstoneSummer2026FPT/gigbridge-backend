using System;

namespace Application.Features.Profiles.ClientProfile.GetClientProfile.DTOs;

public class ClientProfileDetailDto
{
    public Guid ClientProfilesId { get; set; }
    public Guid UserId { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyWebsite { get; set; }
    public int? CompanySize { get; set; }
    public string? Industry { get; set; }
    public string? CompanyDescription { get; set; }
    public string? Location { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserAvatar { get; set; }
    public int EloPoints { get; set; }
}
