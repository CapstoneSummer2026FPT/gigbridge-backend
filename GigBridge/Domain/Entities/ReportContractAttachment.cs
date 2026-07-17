using System;

namespace Domain.Entities;

public partial class ReportContractAttachment
{
    public Guid ReportContractAttachmentId { get; set; }

    public Guid ReportContractId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Identifies who uploaded this attachment.
    /// NULL = legacy records (assumed reporter).
    /// </summary>
    public Guid? UploadedByUserId { get; set; }

    public virtual ReportContract ReportContract { get; set; } = null!;
}
