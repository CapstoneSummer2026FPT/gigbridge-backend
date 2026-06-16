using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Client.Questions;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.Questions.DeleteJobPostQuestion.Commands;

public class DeleteJobPostQuestionCommandHandler
    : IRequestHandler<DeleteJobPostQuestionCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteJobPostQuestionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(
        DeleteJobPostQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var clientProfile = await JobPostQuestionCommandHelper.GetClientProfileAsync(
            _context,
            command.UserId,
            cancellationToken);

        var jobPost = await JobPostQuestionCommandHelper.GetClientOwnedJobPostAsync(
            _context,
            command.JobPostsId,
            clientProfile.ClientProfilesId,
            cancellationToken);

        JobPostQuestionCommandHelper.EnsureDraft(jobPost);

        var question = await _context.Set<JobPostQuestion>()
            .FirstOrDefaultAsync(
                question => question.JobPostQuestionsId == command.JobPostQuestionsId,
                cancellationToken);

        if (question is null)
        {
            throw new NotFoundException("Question does not exist.");
        }

        if (question.JobPostsId != command.JobPostsId)
        {
            throw new BadRequestException("Question does not belong to this job post.");
        }

        _context.Set<JobPostQuestion>().Remove(question);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
