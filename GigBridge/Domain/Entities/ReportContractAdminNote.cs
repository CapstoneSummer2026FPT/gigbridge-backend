namespace Domain.Entities;

public sealed class ReportContractAdminNote
{
    public Guid ReportContractAdminNoteId { get; set; }
    public Guid ReportContractId { get; set; }
    public Guid AdminUserId { get; set; }
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public ReportContract ReportContract { get; set; } = null!;
    public User AdminUser { get; set; } = null!;
}
