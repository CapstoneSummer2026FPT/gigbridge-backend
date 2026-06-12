using System;

namespace Domain.Entities;

public partial class ProposalAnswer
{
    public Guid ProposalAnswersId { get; set; }

    public Guid ProposalsId { get; set; }

    public Guid JobPostQuestionsId { get; set; }

    public string AnswerText { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Proposal Proposals { get; set; } = null!;

    public virtual JobPostQuestion JobPostQuestions { get; set; } = null!;
}