using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class DisputeEvidence
{
    public Guid DisputeEvidenceId { get; set; }

    public Guid DisputesId { get; set; }

    public Guid? UploadedById { get; set; }

    public string? FileName { get; set; }

    public string? FileUrl { get; set; }

    public long? FileSize { get; set; }

    public string? Description { get; set; }

    public bool IsRequestedByAdmin { get; set; }

    public Guid? RequestGroupId { get; set; }

    public Guid? RequestedByAdminId { get; set; }

    public DateTime? RequestedAt { get; set; }

    public DateTime? Deadline { get; set; }

    public int? RequestTarget { get; set; }

    public bool IsRequestFulfilled { get; set; }

    public Guid? ReviewedByAdminId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Dispute Disputes { get; set; } = null!;

    public virtual User? UploadedBy { get; set; }

    public virtual User? RequestedByAdmin { get; set; }

    public virtual User? ReviewedByAdmin { get; set; }
}
