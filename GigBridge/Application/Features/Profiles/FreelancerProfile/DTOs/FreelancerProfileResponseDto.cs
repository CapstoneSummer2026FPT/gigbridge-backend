using System;

namespace Application.Features.Profiles.FreelancerProfile.DTOs;

public class FreelancerProfileResponseDto
{
    public Guid FreelancerProfilesId { get; set; }
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public string? Bio { get; set; }
    public decimal? HourlyRate { get; set; }
    public int? ExperienceLevel { get; set; }
    public int? Availability { get; set; }
    public string? Location { get; set; }
    public int? ProfileCompletionScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
