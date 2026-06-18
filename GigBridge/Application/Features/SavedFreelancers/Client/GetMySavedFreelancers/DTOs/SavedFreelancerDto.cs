namespace Application.Features.SavedFreelancers.Client.GetMySavedFreelancers.DTOs;

public class SavedFreelancerDto
{
    public Guid SavedFreelancerId { get; set; }

    public Guid FreelancerProfileId { get; set; }

    public Guid FreelancerUserId { get; set; }

    public string? Title { get; set; }

    public string? Bio { get; set; }

    public int? Availability { get; set; }

    public string? Location { get; set; }

    public int? ProfileCompletionScore { get; set; }

    public DateTime FreelancerCreatedAt { get; set; }

    public DateTime SavedAt { get; set; }
}