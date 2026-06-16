using Application.Features.JobPosts.Common.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Common.Questions.GetJobPostQuestions.Queries;

public record GetJobPostQuestionsQuery(
    Guid JobPostsId,
    Guid UserId,
    string Role) : IRequest<IEnumerable<JobPostQuestionDto>>;
