using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Common.Questions.GetJobPostQuestions.Queries;

public class GetJobPostQuestionsQueryHandler
    : IRequestHandler<GetJobPostQuestionsQuery, IEnumerable<JobPostQuestionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetJobPostQuestionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<JobPostQuestionDto>> Handle(
        GetJobPostQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(jobPost => jobPost.JobPostsId == request.JobPostsId, cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        await EnsureCanViewQuestionsAsync(jobPost, request, cancellationToken);

        return await _context.Set<JobPostQuestion>()
            .AsNoTracking()
            .Where(question => question.JobPostsId == request.JobPostsId)
            .OrderBy(question => question.OrderIndex)
            .Select(question => new JobPostQuestionDto(
                question.JobPostQuestionsId,
                question.JobPostsId,
                question.QuestionText,
                question.OrderIndex,
                question.IsRequired,
                question.CreatedAt,
                question.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureCanViewQuestionsAsync(
        JobPost jobPost,
        GetJobPostQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.Role, nameof(UserRole.Client), StringComparison.OrdinalIgnoreCase))
        {
            var clientProfile = await _context.Set<ClientProfile>()
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

            if (clientProfile is null)
            {
                throw new NotFoundException("Client profile does not exist.");
            }

            if (jobPost.ClientProfilesId != clientProfile.ClientProfilesId)
            {
                throw new ForbiddenAccessException("You do not have permission to view these questions.");
            }

            return;
        }

        if (string.Equals(request.Role, nameof(UserRole.Freelancer), StringComparison.OrdinalIgnoreCase))
        {
            if (jobPost.Status == 1 && (jobPost.Visibility is null or 0))
            {
                return;
            }

            throw new ForbiddenAccessException("You do not have permission to view these questions.");
        }

        throw new ForbiddenAccessException("You do not have permission to view these questions.");
    }
}
