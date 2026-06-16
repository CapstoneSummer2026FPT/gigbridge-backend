using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.Questions;
using Application.Features.JobPosts.Common.DTOs;
using Domain.Entities;
using MediatR;

namespace Application.Features.JobPosts.Client.Questions.CreateJobPostQuestion.Commands;

public class CreateJobPostQuestionCommandHandler
    : IRequestHandler<CreateJobPostQuestionCommand, JobPostQuestionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public CreateJobPostQuestionCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<JobPostQuestionDto> Handle(
        CreateJobPostQuestionCommand command,
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

        var now = _dateTimeService.UtcNow;
        var question = new JobPostQuestion
        {
            JobPostQuestionsId = Guid.NewGuid(),
            JobPostsId = command.JobPostsId,
            QuestionText = command.Request.QuestionText!.Trim(),
            OrderIndex = command.Request.OrderIndex,
            IsRequired = command.Request.IsRequired,
            CreatedAt = now
        };

        _context.Set<JobPostQuestion>().Add(question);
        await _context.SaveChangesAsync(cancellationToken);

        return JobPostQuestionCommandHelper.ToDto(question);
    }
}
