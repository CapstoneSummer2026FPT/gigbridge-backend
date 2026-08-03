namespace Domain.Entities;

public sealed class ProposalAdminNote
{
    public Guid ProposalAdminNoteId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid AdminUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public Proposal Proposal { get; set; } = null!;
    public User AdminUser { get; set; } = null!;
}
