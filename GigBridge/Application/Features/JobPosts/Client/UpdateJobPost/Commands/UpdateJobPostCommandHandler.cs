using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Features.JobPosts.Client.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.UpdateJobPost.Commands;

public class UpdateJobPostCommandHandler : IRequestHandler<UpdateJobPostCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IContentModerationService _contentModerationService;
    private readonly IAiServiceClient? _aiServiceClient;

    public UpdateJobPostCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IContentModerationService contentModerationService)
        : this(context, dateTimeService, contentModerationService, null)
    {
    }

    public UpdateJobPostCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IContentModerationService contentModerationService,
        IAiServiceClient? aiServiceClient)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _contentModerationService = contentModerationService;
        _aiServiceClient = aiServiceClient;
    }

    public async Task<bool> Handle(UpdateJobPostCommand command, CancellationToken cancellationToken)
    {
        JobPostContentModerationGuard.EnsureAllowed(
            _contentModerationService,
            command.Request.Title,
            command.Request.Description);

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

        if (jobPost.Visibility == 3)
        {
            throw new BadRequestException("This job post has been locked by an admin and cannot be updated.");
        }

        var normalizedSkills = await JobPostSkillNormalizer.NormalizeAsync(
            _context,
            command.Request.MajorCategoryId,
            command.Request.SkillIds,
            command.Request.CustomSkillNames,
            cancellationToken);

        UpdateJobPost(jobPost, command, normalizedSkills);

        await UpdateJobPostSkills(jobPost, normalizedSkills.SkillIds, cancellationToken);

        var activeDefinition = await _context.Set<AiInterviewDefinition>()
            .FirstOrDefaultAsync(
                x => x.JobPostId == jobPost.JobPostsId &&
                     x.Status == AiInterviewDefinitionStatus.Active,
                cancellationToken);

        if (activeDefinition != null && _aiServiceClient != null)
        {
            var systemSkillNames = await _context.Set<Skill>()
                .AsNoTracking()
                .Where(s => normalizedSkills.SkillIds.Contains(s.SkillsId))
                .Select(s => s.Name)
                .ToListAsync(cancellationToken);

            var skills = systemSkillNames.Concat(normalizedSkills.CustomSkillNames)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var registered = await _aiServiceClient.CreateInterviewDefinitionAsync(
                new AiInterviewDefinitionRequestDto
                {
                    JobId = jobPost.JobPostsId.ToString(),
                    JobTitle = jobPost.Title,
                    JobDescription = jobPost.Description,
                    JobSkills = skills,
                    Mode = activeDefinition.Mode,
                    Language = activeDefinition.Language,
                    QuestionCount = activeDefinition.QuestionCount
                },
                cancellationToken);

            activeDefinition.ExternalReference = registered.DefinitionReference;
            activeDefinition.UpdatedAt = _dateTimeService.UtcNow;
        }

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
        jobPost.Location = null;
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
