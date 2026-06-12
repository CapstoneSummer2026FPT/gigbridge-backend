using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.Questions;
using Application.Features.JobPosts.Common.DTOs;
using Domain.Entities;
using MediatR;

namespace Application.Features.JobPosts.Client.Questions.CreateBulkJobPostQuestions.Commands;

public class CreateBulkJobPostQuestionsCommandHandler
    : IRequestHandler<CreateBulkJobPostQuestionsCommand, IEnumerable<JobPostQuestionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public CreateBulkJobPostQuestionsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<IEnumerable<JobPostQuestionDto>> Handle(
        CreateBulkJobPostQuestionsCommand command,
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
        var questions = command.Request.Questions!
            .Select(request => new JobPostQuestion
            {
                JobPostQuestionsId = Guid.NewGuid(),
                JobPostsId = command.JobPostsId,
                QuestionText = request.QuestionText!.Trim(),
                OrderIndex = request.OrderIndex,
                IsRequired = request.IsRequired,
                CreatedAt = now
            })
            .ToList();

        _context.Set<JobPostQuestion>().AddRange(questions);
        await _context.SaveChangesAsync(cancellationToken);

        return questions
            .OrderBy(question => question.OrderIndex)
            .Select(JobPostQuestionCommandHelper.ToDto)
            .ToList();
    }
}
