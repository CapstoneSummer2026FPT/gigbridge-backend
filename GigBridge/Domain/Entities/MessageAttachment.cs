using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class MessageAttachment
{
    public Guid MessageAttachmentsId { get; set; }

    public Guid MessagesId { get; set; }

    public string FileName { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public string StorageProvider { get; set; } = null!;

    public string? StorageObjectKey { get; set; }

    public string MimeType { get; set; } = null!;

    public string? FileExtension { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Message Messages { get; set; } = null!;
}
