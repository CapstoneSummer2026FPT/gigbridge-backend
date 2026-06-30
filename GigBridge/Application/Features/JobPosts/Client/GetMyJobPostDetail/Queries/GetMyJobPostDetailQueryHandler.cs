using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Client.Common;
using Application.Features.JobPosts.Client.GetMyJobPostDetail.DTOs;
using Application.Features.JobPosts.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.GetMyJobPostDetail.Queries;

public class GetMyJobPostDetailQueryHandler
    : IRequestHandler<GetMyJobPostDetailQuery, GetMyJobPostDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetMyJobPostDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetMyJobPostDetailDto> Handle(
        GetMyJobPostDetailQuery request,
        CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .Where(jobPost =>
                jobPost.JobPostsId == request.JobPostId &&
                jobPost.ClientProfilesId == clientProfile.ClientProfilesId)
            .Select(jobPost => new GetMyJobPostDetailDto
            {
                JobPostsId = jobPost.JobPostsId,
                ClientProfilesId = jobPost.ClientProfilesId,

                Title = jobPost.Title,
                Description = jobPost.Description,

                MajorCategoryId = jobPost.MajorCategoryId,

                MajorId = jobPost.MajorCategory != null
                    ? jobPost.MajorCategory.MajorId
                    : null,

                MajorName = jobPost.MajorCategory != null
                    ? jobPost.MajorCategory.Major.Name
                    : null,

                CategoryId = jobPost.MajorCategory != null
                    ? jobPost.MajorCategory.CategoryId
                    : null,

                CategoryName = jobPost.MajorCategory != null
                    ? jobPost.MajorCategory.Category.Name
                    : null,

                BudgetMin = jobPost.BudgetMin,
                BudgetMax = jobPost.BudgetMax,
                Currency = jobPost.Currency,
                EstimatedDuration = jobPost.EstimatedDuration,
                Location = jobPost.Location,
                Visibility = jobPost.Visibility,
                Status = jobPost.Status,
                EndDate = jobPost.EndDate,
                CreatedAt = jobPost.CreatedAt,
                UpdatedAt = jobPost.UpdatedAt,

                Skills = jobPost.JobPostSkills
                    .Select(jobPostSkill => new JobPostSkillDto(
                        jobPostSkill.SkillsId,
                        jobPostSkill.Skills.Name))
                    .ToList(),

                CustomSkillNames = jobPost.CustomSkillNames.ToList(),

                Attachments = jobPost.JobPostAttachments
                    .Select(attachment => new AttachmentDto(
                        attachment.JobPostAttachmentsId,
                        attachment.FileUrl,
                        attachment.FileName))
                    .ToList(),

                ProposalCount = jobPost.Proposals.Count(proposal => proposal.Status != 0)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        await JobPostSetupProgressBuilder.ApplyAsync(_context, jobPost, cancellationToken);

        return jobPost;
    }
}
