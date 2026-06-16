namespace Application.Features.Proposals.Freelancer.Answers.UpdateBulkProposalAnswers.DTOs;

public class UpdateBulkProposalAnswersRequest
{
    public List<UpdateBulkProposalAnswerItemRequest>? Answers { get; set; }
}

public class UpdateBulkProposalAnswerItemRequest
{
    public Guid JobPostQuestionId { get; set; }

    public string? AnswerText { get; set; }
}
