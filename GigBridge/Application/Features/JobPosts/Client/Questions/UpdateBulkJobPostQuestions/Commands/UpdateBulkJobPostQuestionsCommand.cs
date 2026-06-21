using Application.Features.JobPosts.Client.Questions.UpdateBulkJobPostQuestions.DTOs;
using Application.Features.JobPosts.Common.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.Questions.UpdateBulkJobPostQuestions.Commands;

public record UpdateBulkJobPostQuestionsCommand(
    Guid JobPostsId,
    Guid UserId,
    UpdateBulkJobPostQuestionsRequest Request) : IRequest<IEnumerable<JobPostQuestionDto>>;
