using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Domain.Entities;

namespace Application.Features.WorkExperiences.Common;

internal static class WorkExperienceMapping
{
    public static WorkExperienceDto ToDto(this WorkExperience experience) => new()
    {
        WorkExperienceId = experience.WorkExperiencesId,
        CompanyName = experience.CompanyName,
        JobTitle = experience.Title,
        Description = experience.Description,
        StartDate = experience.StartDate.ToString("yyyy-MM-dd"),
        EndDate = experience.EndDate?.ToString("yyyy-MM-dd")
    };

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
