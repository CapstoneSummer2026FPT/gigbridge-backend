using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.CreateJobPost.Commands;

public class CreateJobPostCommandHandler : IRequestHandler<CreateJobPostCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public CreateJobPostCommandHandler(IApplicationDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<Guid> Handle(CreateJobPostCommand command, CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        var jobPost = CreateJobPost(command, clientProfile.ClientProfilesId);
        _context.Set<JobPost>().Add(jobPost);
        _context.Set<Contract>().Add(CreateDraftContract(jobPost));
        await _context.SaveChangesAsync(cancellationToken);

        return jobPost.JobPostsId;
    }

    private JobPost CreateJobPost(CreateJobPostCommand command, Guid clientProfileId)
    {
        var request = command.Request;
        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfileId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CategoryId = request.CategoryId,
            BudgetMin = request.BudgetMin,
            BudgetMax = request.BudgetMax,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim(),
            EstimatedDuration = request.EstimatedDuration,
            MaxHires = request.MaxHires,
            Location = request.Location,
            Visibility = request.Visibility ?? 0,
            EndDate = request.EndDate,
            Status = 0,
            CreatedAt = _dateTimeService.UtcNow
        };

        foreach (var skillId in (request.SkillIds ?? []).Distinct())
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
