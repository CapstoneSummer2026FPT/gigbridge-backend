using Domain.Entities;
using Domain.Enums;

namespace Test_Gigbridge_Backend.Support;

public static class TestData
{
    public static User User(Guid userId, UserRole role, string fullName = "Test User")
    {
        return new User
        {
            UserId = userId,
            FullName = fullName,
            Email = $"{userId:N}@example.com",
            Role = (int)role,
            IsActive = true,
            IsEmailVerified = true,
            IsSetup = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static ClientProfile ClientProfile(Guid userId, Guid? clientProfileId = null)
    {
        return new ClientProfile
        {
            ClientProfilesId = clientProfileId ?? Guid.NewGuid(),
            UserId = userId,
            CompanyName = "Client Co",
            CreatedAt = DateTime.UtcNow
        };
    }

    public static FreelancerProfile FreelancerProfile(Guid userId, Guid? freelancerProfileId = null)
    {
        return new FreelancerProfile
        {
            FreelancerProfilesId = freelancerProfileId ?? Guid.NewGuid(),
            UserId = userId,
            Title = "Developer",
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Skill Skill(Guid? skillId = null, string name = "C#")
    {
        return new Skill
        {
            SkillsId = skillId ?? Guid.NewGuid(),
            CategoriesId = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static JobPost JobPost(
        Guid clientProfileId,
        Guid? jobPostId = null,
        string title = "Build a booking module",
        int status = 1,
        int? visibility = 0,
        DateTime? createdAt = null)
    {
        return new JobPost
        {
            JobPostsId = jobPostId ?? Guid.NewGuid(),
            ClientProfilesId = clientProfileId,
            Title = title,
            Description = "Create booking workflow and notification logic.",
            BudgetType = 0,
            BudgetMin = 500m,
            BudgetMax = 1000m,
            Currency = "VND",
            EstimatedDuration = "2 weeks",
            MaxHires = 1,
            ExperienceLevelRequired = 1,
            LocationType = 0,
            Location = "Remote",
            Status = status,
            Visibility = visibility,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    public static JobPostSkill JobPostSkill(Guid jobPostId, Guid skillId)
    {
        return new JobPostSkill
        {
            JobPostSkillsId = Guid.NewGuid(),
            JobPostsId = jobPostId,
            SkillsId = skillId
        };
    }

    public static Proposal Proposal(
        Guid jobPostId,
        Guid freelancerProfileId,
        Guid? proposalId = null,
        int status = 0,
        DateTime? submittedAt = null)
    {
        return new Proposal
        {
            ProposalsId = proposalId ?? Guid.NewGuid(),
            JobPostsId = jobPostId,
            FreelancerProfilesId = freelancerProfileId,
            CoverLetter = "I can deliver this feature with clean architecture and clear communication throughout the project.",
            ProposedRate = 500m,
            ProposedDuration = "10 days",
            Status = status,
            SubmittedAt = submittedAt ?? DateTime.UtcNow
        };
    }
}
