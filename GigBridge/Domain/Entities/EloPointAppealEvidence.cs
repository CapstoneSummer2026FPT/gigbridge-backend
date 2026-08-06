using System;

namespace Domain.Entities;

/// <summary>
/// Attached file supporting an Elo appeal. Files are stored privately via
/// IMediaService.UploadPrivateFileAsync and served through a signed URL.
/// </summary>
public partial class EloPointAppealEvidence
{
    public Guid EloPointAppealEvidenceId { get; set; }

    public Guid EloPointAppealId { get; set; }

    public Guid UploadedById { get; set; }

    public string? FileName { get; set; }

    public string? FileUrl { get; set; }

    public long? FileSize { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual EloPointAppeal EloPointAppeal { get; set; } = null!;

    public virtual User UploadedBy { get; set; } = null!;
}
