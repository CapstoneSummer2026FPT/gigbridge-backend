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

    public virtual ReportContract ReportContract { get; set; } = null!;
}
