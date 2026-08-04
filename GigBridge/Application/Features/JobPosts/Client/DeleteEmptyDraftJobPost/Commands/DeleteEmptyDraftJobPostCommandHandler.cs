using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.DeleteEmptyDraftJobPost.Commands;

public sealed class DeleteEmptyDraftJobPostCommandHandler
    : IRequestHandler<DeleteEmptyDraftJobPostCommand, bool>
{
    private const int DraftStatus = 0;
    private const int PublicVisibility = 0;

    private static readonly string[] EmptyDraftTitles =
    {
        "Untitled Job Post",
        "Untitled Draft"
    };

    private readonly IApplicationDbContext _context;

    public DeleteEmptyDraftJobPostCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        DeleteEmptyDraftJobPostCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        var jobPost = await _context.Set<JobPost>()
            .Include(jobPost => jobPost.Contract)
            .Include(jobPost => jobPost.EsignDocuments)
            .Include(jobPost => jobPost.JobPostAttachments)
            .Include(jobPost => jobPost.JobPostQuestions)
            .Include(jobPost => jobPost.JobPostSkills)
            .Include(jobPost => jobPost.Proposals)
            .FirstOrDefaultAsync(
                jobPost =>
                    jobPost.JobPostsId == command.JobPostId &&
                    jobPost.ClientProfilesId == clientProfile.ClientProfilesId,
                cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist or you do not have permission to delete it.");
        }

        EnsureEmptyDraft(jobPost);

        _context.Set<JobPost>().Remove(jobPost);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void EnsureEmptyDraft(JobPost jobPost)
    {
        if (jobPost.Status != DraftStatus)
        {
            throw new BadRequestException("Only draft job posts can be deleted by this endpoint.");
        }

        if (HasMeaningfulContent(jobPost))
        {
            throw new BadRequestException("This draft contains information and cannot be deleted as an empty draft.");
        }
    }

    private static bool HasMeaningfulContent(JobPost jobPost)
    {
        return HasMeaningfulTitle(jobPost.Title) ||
               !string.IsNullOrWhiteSpace(jobPost.Description) ||
               jobPost.MajorCategoryId.HasValue ||
               jobPost.BudgetMin.HasValue ||
               jobPost.BudgetMax.HasValue ||
               HasMeaningfulCurrency(jobPost.Currency) ||
               !string.IsNullOrWhiteSpace(jobPost.EstimatedDuration) ||
               !string.IsNullOrWhiteSpace(jobPost.Location) ||
               jobPost.EndDate.HasValue ||
               (jobPost.Visibility.HasValue && jobPost.Visibility.Value != PublicVisibility) ||
               jobPost.IsAigenerated == true ||
               jobPost.CustomSkillNames.Any(skillName => !string.IsNullOrWhiteSpace(skillName)) ||
               jobPost.EsignDocuments.Count > 0 ||
               jobPost.JobPostAttachments.Count > 0 ||
               jobPost.JobPostQuestions.Count > 0 ||
               jobPost.JobPostSkills.Count > 0 ||
               jobPost.Proposals.Count > 0;
    }

    private static bool HasMeaningfulTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        return !EmptyDraftTitles.Contains(title.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasMeaningfulCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return false;
        }

        return !string.Equals(currency.Trim(), "USD", StringComparison.OrdinalIgnoreCase);
    }
}
