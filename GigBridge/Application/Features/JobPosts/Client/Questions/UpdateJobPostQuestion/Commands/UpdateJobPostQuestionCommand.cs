using Application.Features.JobPosts.Client.Questions.UpdateJobPostQuestion.DTOs;
using Application.Features.JobPosts.Common.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.Questions.UpdateJobPostQuestion.Commands;

public record UpdateJobPostQuestionCommand(
    Guid JobPostsId,
    Guid JobPostQuestionsId,
    Guid UserId,
    UpdateJobPostQuestionRequest Request) : IRequest<JobPostQuestionDto>;
