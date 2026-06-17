using Application.Common.Exceptions;
using Application.Common.Interfaces;
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
                CategoryId = jobPost.CategoryId,
                CategoryName = jobPost.Category != null ? jobPost.Category.Name : null,
                BudgetMin = jobPost.BudgetMin,
                BudgetMax = jobPost.BudgetMax,
                Currency = jobPost.Currency,
                EstimatedDuration = jobPost.EstimatedDuration,
                MaxHires = jobPost.MaxHires,
                Location = jobPost.Location,
                Visibility = jobPost.Visibility,
                Status = jobPost.Status,
                EndDate = jobPost.EndDate,
                CreatedAt = jobPost.CreatedAt,
                UpdatedAt = jobPost.UpdatedAt,
                Skills = jobPost.JobPostSkills
                    .Where(jobPostSkill => jobPostSkill.Skills != null)
                    .Select(jobPostSkill => new JobPostSkillDto(
                        jobPostSkill.SkillsId,
                        jobPostSkill.Skills.Name))
                    .ToList(),
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

        return jobPost;
    }
}
