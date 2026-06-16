namespace Application.Features.JobPosts.Common.DTOs;

public record JobPostQuestionDto(
    Guid JobPostQuestionsId,
    Guid JobPostsId,
    string QuestionText,
    int OrderIndex,
    bool IsRequired,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
