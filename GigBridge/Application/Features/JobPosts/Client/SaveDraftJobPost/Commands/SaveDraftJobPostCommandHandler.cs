using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.JobPosts.Common.ContentModeration;
using Application.Features.JobPosts.Client.Common;
using Application.Features.JobPosts.Client.SaveDraftJobPost.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.SaveDraftJobPost.Commands;

public sealed class SaveDraftJobPostCommandHandler
    : IRequestHandler<SaveDraftJobPostCommand, bool>
{
    private const int DraftStatus = 0;
    private const int PublicVisibility = 0;
    private const string DefaultDraftTitle = "Untitled Job Post";

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IContentModerationService _contentModerationService;

    public SaveDraftJobPostCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IContentModerationService contentModerationService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _contentModerationService = contentModerationService;
    }

    public async Task<bool> Handle(
        SaveDraftJobPostCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        var jobPost = await _context.Set<JobPost>()
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

        if (jobPost.Status != DraftStatus)
        {
            throw new BadRequestException("Only draft job posts can be saved as draft.");
        }

        JobPostContentModerationGuard.EnsureAllowed(
            _contentModerationService,
            command.Request.Title,
            command.Request.Description);

        var normalizedSkills = await JobPostSkillNormalizer.NormalizeAsync(
            _context,
            command.Request.MajorCategoryId,
            command.Request.SkillIds,
            command.Request.CustomSkillNames,
            cancellationToken);

        ApplyDraftFields(jobPost, command.Request, normalizedSkills);
        await ReplaceJobPostSkills(jobPost.JobPostsId, normalizedSkills.SkillIds, cancellationToken);
        await ReplaceQuestionsIfProvided(jobPost.JobPostsId, command.Request.Questions, cancellationToken);
        await ReplaceMilestonePlansIfProvided(jobPost, command.Request.MilestonePlans, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task ReplaceMilestonePlansIfProvided(
        JobPost jobPost,
        List<Application.Features.JobPosts.Common.DTOs.JobPostMilestonePlanDto>? requests,
        CancellationToken cancellationToken)
    {
        if (requests is null) return;

        var existing = await _context.Set<JobPostMilestonePlan>()
            .Where(item => item.JobPostsId == jobPost.JobPostsId)
            .ToListAsync(cancellationToken);
        _context.Set<JobPostMilestonePlan>().RemoveRange(existing);

        var now = _dateTimeService.UtcNow;
        foreach (var request in requests.OrderBy(item => item.OrderIndex).Select((item, index) => (item, index)))
        {
            var milestone = new JobPostMilestonePlan
            {
                JobPostMilestonePlanId = Guid.NewGuid(),
                JobPostsId = jobPost.JobPostsId,
                Title = request.item.Title?.Trim() ?? string.Empty,
                Description = Clean(request.item.Description),
                Amount = request.item.Amount,
                EstimatedDuration = Clean(request.item.EstimatedDuration),
                DueDate = request.item.DueDate,
                Deliverables = Clean(request.item.Deliverables),
                AcceptanceCriteria = Clean(request.item.AcceptanceCriteria),
                OrderIndex = request.index,
                CreatedAt = now,
                UpdatedAt = now
            };
            milestone.WorkItems = request.item.WorkItems
                .OrderBy(item => item.OrderIndex)
                .Select((item, index) => new JobPostWorkItem
                {
                    JobPostWorkItemId = Guid.NewGuid(),
                    JobPostMilestonePlanId = milestone.JobPostMilestonePlanId,
                    Title = item.Title?.Trim() ?? string.Empty,
                    Description = Clean(item.Description),
                    Deliverables = Clean(item.Deliverables),
                    EstimatedDuration = Clean(item.EstimatedDuration),
                    OrderIndex = index
                })
                .ToList();
            _context.Set<JobPostMilestonePlan>().Add(milestone);
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ApplyDraftFields(
        JobPost jobPost,
        SaveDraftJobPostRequest request,
        NormalizedJobPostSkills normalizedSkills)
    {
        jobPost.Title = string.IsNullOrWhiteSpace(request.Title)
            ? DefaultDraftTitle
            : request.Title.Trim();
        jobPost.Description = string.IsNullOrWhiteSpace(request.Description)
            ? string.Empty
            : request.Description.Trim();
        jobPost.MajorCategoryId = request.MajorCategoryId;
        jobPost.BudgetMin = request.BudgetMin;
        jobPost.BudgetMax = request.BudgetMax;
        jobPost.Currency = string.IsNullOrWhiteSpace(request.Currency)
            ? "USD"
            : request.Currency.Trim();
        jobPost.EstimatedDuration = string.IsNullOrWhiteSpace(request.EstimatedDuration)
            ? null
            : request.EstimatedDuration.Trim();
        jobPost.Location = null;
        jobPost.Visibility = request.Visibility ?? PublicVisibility;
        jobPost.EndDate = request.EndDate;
        jobPost.IsAigenerated = request.IsAigenerated;
        jobPost.CustomSkillNames = normalizedSkills.CustomSkillNames;
        jobPost.UpdatedAt = _dateTimeService.UtcNow;
    }

    private async Task ReplaceJobPostSkills(
        Guid jobPostId,
        IReadOnlyCollection<Guid> skillIds,
        CancellationToken cancellationToken)
    {
        var existingSkills = await _context.Set<JobPostSkill>()
            .Where(jobPostSkill => jobPostSkill.JobPostsId == jobPostId)
            .ToListAsync(cancellationToken);

        _context.Set<JobPostSkill>().RemoveRange(existingSkills);

        foreach (var skillId in skillIds)
        {
            _context.Set<JobPostSkill>().Add(new JobPostSkill
            {
                JobPostSkillsId = Guid.NewGuid(),
                JobPostsId = jobPostId,
                SkillsId = skillId
            });
        }
    }

    private async Task ReplaceQuestionsIfProvided(
        Guid jobPostId,
        List<SaveDraftJobPostQuestionRequest>? questionRequests,
        CancellationToken cancellationToken)
    {
        if (questionRequests is null)
        {
            return;
        }

        var existingQuestions = await _context.Set<JobPostQuestion>()
            .Where(question => question.JobPostsId == jobPostId)
            .ToListAsync(cancellationToken);

        _context.Set<JobPostQuestion>().RemoveRange(existingQuestions);

        var now = _dateTimeService.UtcNow;
        var newQuestions = questionRequests
            .Where(question => !string.IsNullOrWhiteSpace(question.QuestionText))
            .OrderBy(question => question.OrderIndex)
            .Select((question, index) => new JobPostQuestion
            {
                JobPostQuestionsId = Guid.NewGuid(),
                JobPostsId = jobPostId,
                QuestionText = question.QuestionText!.Trim(),
                OrderIndex = index,
                IsRequired = question.IsRequired,
                CreatedAt = now
            })
            .ToList();

        _context.Set<JobPostQuestion>().AddRange(newQuestions);
    }
}
