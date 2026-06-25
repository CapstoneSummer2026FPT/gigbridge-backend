using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.UpdateJobPost.Commands;

public class UpdateJobPostCommandHandler : IRequestHandler<UpdateJobPostCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateJobPostCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<bool> Handle(UpdateJobPostCommand command, CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(
                profile => profile.UserId == command.UserId,
                cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        var jobPost = await _context.Set<JobPost>()
            .Include(jobPost => jobPost.JobPostSkills)
            .FirstOrDefaultAsync(
                jobPost =>
                    jobPost.JobPostsId == command.JobPostId &&
                    jobPost.ClientProfilesId == clientProfile.ClientProfilesId,
                cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist or you do not have permission to update it.");
        }

        var normalizedSkills = await JobPostSkillNormalizer.NormalizeAsync(
            _context,
            command.Request.MajorCategoryId,
            command.Request.SkillIds,
            command.Request.CustomSkillNames,
            cancellationToken);

        UpdateJobPost(jobPost, command, normalizedSkills);

        await UpdateJobPostSkills(jobPost, normalizedSkills.SkillIds, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private void UpdateJobPost(
        JobPost jobPost,
        UpdateJobPostCommand command,
        NormalizedJobPostSkills normalizedSkills)
    {
        var request = command.Request;

        jobPost.Title = request.Title.Trim();
        jobPost.Description = request.Description.Trim();

        jobPost.MajorCategoryId = request.MajorCategoryId;

        jobPost.BudgetMin = request.BudgetMin;
        jobPost.BudgetMax = request.BudgetMax;
        jobPost.Currency = string.IsNullOrWhiteSpace(request.Currency)
            ? "USD"
            : request.Currency.Trim();

        jobPost.EstimatedDuration = request.EstimatedDuration;
        jobPost.Location = request.Location;
        jobPost.Visibility = request.Visibility!.Value;
        jobPost.EndDate = request.EndDate;

        jobPost.CustomSkillNames = normalizedSkills.CustomSkillNames;

        jobPost.UpdatedAt = _dateTimeService.UtcNow;
    }

    private async Task UpdateJobPostSkills(
        JobPost jobPost,
        IReadOnlyList<Guid> skillIds,
        CancellationToken cancellationToken)
    {
        var oldSkills = await _context.Set<JobPostSkill>()
            .Where(jobPostSkill => jobPostSkill.JobPostsId == jobPost.JobPostsId)
            .ToListAsync(cancellationToken);

        _context.Set<JobPostSkill>().RemoveRange(oldSkills);

        foreach (var skillId in skillIds)
        {
            _context.Set<JobPostSkill>().Add(new JobPostSkill
            {
                JobPostSkillsId = Guid.NewGuid(),
                JobPostsId = jobPost.JobPostsId,
                SkillsId = skillId
            });
        }
    }
}
