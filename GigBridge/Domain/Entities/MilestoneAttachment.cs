using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class MilestoneAttachment
{
    public Guid MilestoneAttachmentsId { get; set; }

    public Guid MilestonesId { get; set; }

    public string FileName { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public long? FileSize { get; set; }

    public int SourceType { get; set; }

    public string? MimeType { get; set; }

    public Guid? UploadedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Milestone Milestones { get; set; } = null!;

    public virtual User? UploadedByUser { get; set; }
}
