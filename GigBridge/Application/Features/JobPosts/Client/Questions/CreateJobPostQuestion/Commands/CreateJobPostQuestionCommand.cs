using Application.Features.JobPosts.Client.Questions.CreateJobPostQuestion.DTOs;
using Application.Features.JobPosts.Common.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.Questions.CreateJobPostQuestion.Commands;

public record CreateJobPostQuestionCommand(
    Guid JobPostsId,
    Guid UserId,
    CreateJobPostQuestionRequest Request) : IRequest<JobPostQuestionDto>;
