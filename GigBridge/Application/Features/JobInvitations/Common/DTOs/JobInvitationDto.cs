namespace Application.Features.JobInvitations.Common.DTOs;

public sealed class JobInvitationDto
{
    public Guid JobInvitationId { get; set; }

    public Guid JobInvitationsId { get; set; }

    public Guid JobPostId { get; set; }

    public Guid JobPostsId { get; set; }

    public Guid ClientProfileId { get; set; }

    public Guid ClientProfilesId { get; set; }

    public Guid FreelancerProfileId { get; set; }

    public Guid FreelancerProfilesId { get; set; }

    public Guid ClientUserId { get; set; }

    public Guid FreelancerUserId { get; set; }

    public Guid? ProposalId { get; set; }

    public Guid? ProposalsId { get; set; }

    public int Status { get; set; }

    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ViewedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? DeclineReason { get; set; }

    public string JobTitle { get; set; } = string.Empty;

    public string? JobDescription { get; set; }

    public Guid? MajorCategoryId { get; set; }

    public Guid? MajorId { get; set; }

    public string? MajorName { get; set; }

    public Guid? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public List<JobInvitationSkillDto> Skills { get; set; } = new();

    public List<string> CustomSkillNames { get; set; } = new();

    public decimal? BudgetMin { get; set; }

    public decimal? BudgetMax { get; set; }

    public string? Currency { get; set; }

    public string? EstimatedDuration { get; set; }

    public int? MaxHires { get; set; }

    public string? Location { get; set; }

    public int JobStatus { get; set; }

    public int? JobVisibility { get; set; }

    public DateTime? JobEndDate { get; set; }

    public DateTime JobCreatedAt { get; set; }

    public string? ClientName { get; set; }

    public string? ClientCompanyName { get; set; }

    public string? ClientLocation { get; set; }

    public string? FreelancerName { get; set; }

    public string? FreelancerTitle { get; set; }

    public string? FreelancerAvatarUrl { get; set; }

    public string? FreelancerLocation { get; set; }
}
