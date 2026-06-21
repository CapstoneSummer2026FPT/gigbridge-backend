using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.Questions;
using Application.Features.JobPosts.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.Questions.UpdateJobPostQuestionRequired.Commands;

public class UpdateJobPostQuestionRequiredCommandHandler
    : IRequestHandler<UpdateJobPostQuestionRequiredCommand, JobPostQuestionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateJobPostQuestionRequiredCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<JobPostQuestionDto> Handle(
        UpdateJobPostQuestionRequiredCommand command,
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

        question.IsRequired = command.Request.IsRequired;
        question.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return JobPostQuestionCommandHelper.ToDto(question);
    }
}
