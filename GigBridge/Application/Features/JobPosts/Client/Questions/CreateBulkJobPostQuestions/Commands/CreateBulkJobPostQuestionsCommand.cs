using Application.Features.JobPosts.Client.Questions.CreateBulkJobPostQuestions.DTOs;
using Application.Features.JobPosts.Common.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.Questions.CreateBulkJobPostQuestions.Commands;

public record CreateBulkJobPostQuestionsCommand(
    Guid JobPostsId,
    Guid UserId,
    CreateBulkJobPostQuestionsRequest Request) : IRequest<IEnumerable<JobPostQuestionDto>>;
