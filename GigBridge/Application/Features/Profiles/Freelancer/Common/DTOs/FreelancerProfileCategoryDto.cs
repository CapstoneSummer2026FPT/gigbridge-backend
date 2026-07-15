namespace Application.Features.Profiles.FreelancerProfile.Common.DTOs;

public class FreelancerProfileCategoryDto
{
    public Guid MajorCategoryId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
}
