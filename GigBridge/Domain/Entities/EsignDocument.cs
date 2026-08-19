using Domain.Enums.ESign;
using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class EsignDocument
{
    public Guid EsignDocumentsId { get; set; }

    public Guid EsignTemplatesId { get; set; }

    public Guid JobPostsId { get; set; }

    public Guid? ContractsId { get; set; }

    public string DocumentCode { get; set; } = null!;

    /// <summary>
    /// Enum ESignDocumentStatus: 0=Draft, 1=PendingSignatures, 2=PartiallySigned, 3=FullySigned, 4=Expired, 5=Voided
    /// </summary>
    public int Status { get; set; }

    public string? DocumentHash { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public string? ExportedPdfUrl { get; set; }

    public string? FinalizedDocumentFileName { get; set; }

    public long? FinalizedDocumentSizeBytes { get; set; }

    public string? PdfDocumentHash { get; set; }

    public int PdfSignatureCount { get; set; }

    public long? PdfDocumentSizeBytes { get; set; }

    /// <summary>
    /// Bumped every time the paired <see cref="EsignDocumentContent"/> row changes, so callers can
    /// detect content changes without loading the heavy columns themselves.
    /// </summary>
    public int ContentRevision { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Contract? Contracts { get; set; }

    public virtual EsignDocumentContent? Content { get; set; }

    public virtual EsignTemplate EsignTemplates { get; set; } = null!;

    public virtual ICollection<EsignSignature> EsignSignatures { get; set; } = new List<EsignSignature>();

    public virtual JobPost JobPosts { get; set; } = null!;
}
