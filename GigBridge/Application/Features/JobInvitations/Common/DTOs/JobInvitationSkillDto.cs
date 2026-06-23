namespace Application.Features.JobInvitations.Common.DTOs;

public sealed class JobInvitationSkillDto
{
    public Guid SkillId { get; set; }

    public string Name { get; set; } = string.Empty;
}
