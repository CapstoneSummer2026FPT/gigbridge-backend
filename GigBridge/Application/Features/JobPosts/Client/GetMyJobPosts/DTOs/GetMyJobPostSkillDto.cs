namespace Application.Features.JobPosts.Client.GetMyJobPosts.DTOs;

public sealed class GetMyJobPostSkillDto
{
    public Guid SkillId { get; set; }

    public string Name { get; set; } = string.Empty;
}