using MediatR;

namespace Application.Features.JobPosts.Client.Questions.DeleteJobPostQuestion.Commands;

public record DeleteJobPostQuestionCommand(
    Guid JobPostsId,
    Guid JobPostQuestionsId,
    Guid UserId) : IRequest<bool>;
