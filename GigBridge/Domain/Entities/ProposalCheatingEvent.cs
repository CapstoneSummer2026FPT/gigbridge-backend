using System;

namespace Domain.Entities;

public partial class ProposalCheatingEvent
{
    public Guid ProposalCheatingEventsId { get; set; }

    public Guid ProposalsId { get; set; }

    public Guid FreelancerUserId { get; set; }

    public Guid? JobPostQuestionsId { get; set; }

    public int EventType { get; set; }

    public string ClientEventId { get; set; } = null!;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? Metadata { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User FreelancerUser { get; set; } = null!;

    public virtual JobPostQuestion? JobPostQuestions { get; set; }

    public virtual Proposal Proposals { get; set; } = null!;
}
