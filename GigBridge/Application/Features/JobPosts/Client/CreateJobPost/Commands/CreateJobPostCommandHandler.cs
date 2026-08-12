using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.JobPosts.Common.ContentModeration;
using Application.Features.JobPosts.Client.Common;
using Domain.Entities;
using Domain.Enums.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.CreateJobPost.Commands;

public class CreateJobPostCommandHandler : IRequestHandler<CreateJobPostCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IContentModerationService _contentModerationService;

    public CreateJobPostCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IContentModerationService contentModerationService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _contentModerationService = contentModerationService;
    }

    public async Task<Guid> Handle(CreateJobPostCommand command, CancellationToken cancellationToken)
    {
        JobPostContentModerationGuard.EnsureAllowed(
            _contentModerationService,
            command.Request.Title,
            command.Request.Description);

        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        var normalizedSkills = await JobPostSkillNormalizer.NormalizeAsync(
            _context,
            command.Request.MajorCategoryId,
            command.Request.SkillIds,
            command.Request.CustomSkillNames,
            cancellationToken);

        var jobPost = CreateJobPost(command, clientProfile.ClientProfilesId, normalizedSkills);

        _context.Set<JobPost>().Add(jobPost);
        _context.Set<Contract>().Add(CreateDraftContract(jobPost));

        await _context.SaveChangesAsync(cancellationToken);

        return jobPost.JobPostsId;
    }

    private JobPost CreateJobPost(
        CreateJobPostCommand command,
        Guid clientProfileId,
        NormalizedJobPostSkills normalizedSkills)
    {
        var request = command.Request;

        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfileId,

            Title = request.Title.Trim(),
            Description = request.Description.Trim(),

            MajorCategoryId = request.MajorCategoryId,

            BudgetMin = request.BudgetMin,
            BudgetMax = request.BudgetMax,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim(),
            EstimatedDuration = request.EstimatedDuration,
            Location = null,
            Visibility = request.Visibility ?? 0,
            EndDate = request.EndDate,

            CustomSkillNames = normalizedSkills.CustomSkillNames,

            Status = 0,
            CreatedAt = _dateTimeService.UtcNow
        };

        foreach (var skillId in normalizedSkills.SkillIds)
        {
            jobPost.JobPostSkills.Add(new JobPostSkill
            {
                JobPostSkillsId = Guid.NewGuid(),
                JobPostsId = jobPost.JobPostsId,
                SkillsId = skillId
            });
        }

        return jobPost;
    }

    private Contract CreateDraftContract(JobPost jobPost)
    {
        return new Contract
        {
            ContractsId = Guid.NewGuid(),
            JobPostsId = jobPost.JobPostsId,
            ClientProfilesId = jobPost.ClientProfilesId,
            FreelancerProfilesId = null,
            ProposalsId = null,
            Title = jobPost.Title,
            Description = jobPost.Description,
            TotalBudget = jobPost.BudgetMin ?? jobPost.BudgetMax ?? 0m,
            Status = (int)ContractStatus.PendingFreelancerSelection,
            EndDate = jobPost.EndDate.HasValue
                ? DateOnly.FromDateTime(jobPost.EndDate.Value)
                : null,
            CreatedAt = _dateTimeService.UtcNow
        };
    }
}
