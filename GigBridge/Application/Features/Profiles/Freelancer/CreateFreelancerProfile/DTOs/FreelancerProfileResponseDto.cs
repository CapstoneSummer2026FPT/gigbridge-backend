using System;
using Application.Features.Profiles.FreelancerProfile.Common.DTOs;

namespace Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.DTOs;

public class FreelancerProfileResponseDto
{
    public Guid FreelancerProfilesId { get; set; }
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public string? Bio { get; set; }
    public int? Availability { get; set; }
    public string? Location { get; set; }
    public int? ProfileCompletionScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
    public List<FreelancerProfileCategoryDto> Categories { get; set; } = new();
}
