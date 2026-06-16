using Application.Features.JobPosts.Client.Questions.UpdateJobPostQuestionRequired.DTOs;
using Application.Features.JobPosts.Common.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.Questions.UpdateJobPostQuestionRequired.Commands;

public record UpdateJobPostQuestionRequiredCommand(
    Guid JobPostsId,
    Guid JobPostQuestionsId,
    Guid UserId,
    UpdateJobPostQuestionRequiredRequest Request) : IRequest<JobPostQuestionDto>;
