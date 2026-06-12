namespace Application.Features.Proposals.Common.DTOs;

public record ProposalAnswerDto(
    Guid? ProposalAnswersId,
    Guid ProposalsId,
    Guid JobPostQuestionsId,
    string QuestionText,
    int OrderIndex,
    bool IsRequired,
    string? AnswerText,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);
