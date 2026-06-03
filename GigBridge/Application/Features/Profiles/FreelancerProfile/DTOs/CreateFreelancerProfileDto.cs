namespace Application.Features.Profiles.FreelancerProfile.DTOs;

public class CreateFreelancerProfileDto
{
    public string Title { get; set; } = null!;
    public string Bio { get; set; } = null!;
    public decimal HourlyRate { get; set; }
    public int ExperienceLevel { get; set; }
    public int Availability { get; set; }
    public string Location { get; set; } = null!;
}
