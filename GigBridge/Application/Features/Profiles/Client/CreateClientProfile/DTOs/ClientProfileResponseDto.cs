using System;

namespace Application.Features.Profiles.ClientProfile.CreateClientProfile.DTOs;

public class ClientProfileResponseDto
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
}
