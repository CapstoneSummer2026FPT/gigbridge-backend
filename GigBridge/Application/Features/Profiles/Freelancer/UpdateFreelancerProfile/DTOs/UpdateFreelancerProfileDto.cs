namespace Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.DTOs;

public class UpdateFreelancerProfileDto
{
    public string Title { get; set; } = null!;
    public string Bio { get; set; } = null!;
    public int Availability { get; set; }
    public string Location { get; set; } = null!;
}
