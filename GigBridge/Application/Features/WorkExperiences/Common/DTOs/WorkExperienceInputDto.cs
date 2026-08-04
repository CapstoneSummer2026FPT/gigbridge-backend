namespace Application.Features.WorkExperiences.Common.DTOs;

public sealed class WorkExperienceInputDto
{
    public string CompanyName { get; set; } = null!;
    public string JobTitle { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Description { get; set; }
}
