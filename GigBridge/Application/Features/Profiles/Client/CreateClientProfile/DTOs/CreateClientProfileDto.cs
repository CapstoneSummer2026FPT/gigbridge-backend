namespace Application.Features.Profiles.ClientProfile.CreateClientProfile.DTOs;

public class CreateClientProfileDto
{
    public string CompanyName { get; set; } = null!;
    public string? CompanyWebsite { get; set; }
    public int CompanySize { get; set; }
    public string Industry { get; set; } = null!;
    public string? CompanyDescription { get; set; }
    public string Location { get; set; } = null!;
}
