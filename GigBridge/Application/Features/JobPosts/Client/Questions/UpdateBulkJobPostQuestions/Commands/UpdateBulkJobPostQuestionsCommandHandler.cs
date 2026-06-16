using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.Questions;
using Application.Features.JobPosts.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.Questions.UpdateBulkJobPostQuestions.Commands;

public class UpdateBulkJobPostQuestionsCommandHandler
    : IRequestHandler<UpdateBulkJobPostQuestionsCommand, IEnumerable<JobPostQuestionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateBulkJobPostQuestionsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<IEnumerable<JobPostQuestionDto>> Handle(
        UpdateBulkJobPostQuestionsCommand command,
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

        var requestItems = command.Request.Questions!;
        var requestIds = requestItems
            .Select(question => question.JobPostQuestionsId)
            .ToList();

        if (requestIds.Distinct().Count() != requestIds.Count)
        {
            throw new BadRequestException("Questions must not contain duplicate JobPostQuestionsId values.");
        }

        var questions = await _context.Set<JobPostQuestion>()
            .Where(question => requestIds.Contains(question.JobPostQuestionsId))
            .ToListAsync(cancellationToken);

        if (questions.Count != requestIds.Count)
        {
            throw new NotFoundException("One or more questions do not exist.");
        }

        if (questions.Any(question => question.JobPostsId != command.JobPostsId))
        {
            throw new BadRequestException("One or more questions do not belong to this job post.");
        }

        var now = _dateTimeService.UtcNow;
        var questionsById = questions.ToDictionary(question => question.JobPostQuestionsId);

        foreach (var request in requestItems)
        {
            var question = questionsById[request.JobPostQuestionsId];
            question.QuestionText = request.QuestionText!.Trim();
            question.OrderIndex = request.OrderIndex;
            question.IsRequired = request.IsRequired;
            question.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return questions
            .OrderBy(question => question.OrderIndex)
            .Select(JobPostQuestionCommandHelper.ToDto)
            .ToList();
    }
}
