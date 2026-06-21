namespace Application.Features.Proposals.Freelancer.Answers.CreateProposalAnswer.DTOs;

public class CreateProposalAnswerRequest
{
    public Guid JobPostQuestionId { get; set; }

    public string? AnswerText { get; set; }
}
