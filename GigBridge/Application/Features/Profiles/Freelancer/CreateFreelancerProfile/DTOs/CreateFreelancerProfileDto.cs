namespace Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.DTOs;

public class CreateFreelancerProfileDto
{
    public string Title { get; set; } = null!;
    public string Bio { get; set; } = null!;
    public int Availability { get; set; }
    public string Location { get; set; } = null!;
    public Guid MajorId { get; set; }
    public IReadOnlyCollection<Guid> CategoryIds { get; set; } = Array.Empty<Guid>();
}
