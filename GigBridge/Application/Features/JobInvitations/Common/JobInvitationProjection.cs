using Application.Features.JobInvitations.Common.DTOs;
using Domain.Entities;

namespace Application.Features.JobInvitations.Common;

public static class JobInvitationProjection
{
    public static IQueryable<JobInvitationDto> ProjectToJobInvitationDto(this IQueryable<JobInvitation> query)
    {
        return query.Select(invitation => new JobInvitationDto
        {
            JobInvitationId = invitation.JobInvitationsId,
            JobInvitationsId = invitation.JobInvitationsId,
            JobPostId = invitation.JobPostsId,
            JobPostsId = invitation.JobPostsId,
            ClientProfileId = invitation.ClientProfilesId,
            ClientProfilesId = invitation.ClientProfilesId,
            FreelancerProfileId = invitation.FreelancerProfilesId,
            FreelancerProfilesId = invitation.FreelancerProfilesId,
            ClientUserId = invitation.ClientProfiles.UserId,
            FreelancerUserId = invitation.FreelancerProfiles.UserId,
            ProposalId = invitation.ProposalsId,
            ProposalsId = invitation.ProposalsId,
            Status = invitation.Status,
            Message = invitation.Message,
            CreatedAt = invitation.CreatedAt,
            ViewedAt = invitation.ViewedAt,
            RespondedAt = invitation.RespondedAt,
            ExpiresAt = invitation.ExpiresAt,
            DeclineReason = invitation.DeclineReason,
            JobTitle = invitation.JobPosts.Title,
            JobDescription = invitation.JobPosts.Description,
            MajorCategoryId = invitation.JobPosts.MajorCategoryId,
            MajorId = invitation.JobPosts.MajorCategory != null
                ? invitation.JobPosts.MajorCategory.MajorId
                : null,
            MajorName = invitation.JobPosts.MajorCategory != null
                ? invitation.JobPosts.MajorCategory.Major.Name
                : null,
            CategoryId = invitation.JobPosts.MajorCategory != null
                ? invitation.JobPosts.MajorCategory.CategoryId
                : null,
            CategoryName = invitation.JobPosts.MajorCategory != null
                ? invitation.JobPosts.MajorCategory.Category.Name
                : null,
            Skills = invitation.JobPosts.JobPostSkills
                .Select(jobPostSkill => new JobInvitationSkillDto
                {
                    SkillId = jobPostSkill.SkillsId,
                    Name = jobPostSkill.Skills.Name
                })
                .ToList(),
            CustomSkillNames = invitation.JobPosts.CustomSkillNames.ToList(),
            BudgetMin = invitation.JobPosts.BudgetMin,
            BudgetMax = invitation.JobPosts.BudgetMax,
            Currency = invitation.JobPosts.Currency,
            EstimatedDuration = invitation.JobPosts.EstimatedDuration,
            MaxHires = invitation.JobPosts.MaxHires,
            Location = invitation.JobPosts.Location,
            JobStatus = invitation.JobPosts.Status,
            JobVisibility = invitation.JobPosts.Visibility,
            JobEndDate = invitation.JobPosts.EndDate,
            JobCreatedAt = invitation.JobPosts.CreatedAt,
            ClientName = invitation.ClientProfiles.User.FullName,
            ClientCompanyName = invitation.ClientProfiles.CompanyName,
            ClientLocation = invitation.ClientProfiles.Location,
            FreelancerName = invitation.FreelancerProfiles.User.FullName,
            FreelancerTitle = invitation.FreelancerProfiles.Title,
            FreelancerAvatarUrl = invitation.FreelancerProfiles.User.Avatar,
            FreelancerLocation = invitation.FreelancerProfiles.Location
        });
    }
}
