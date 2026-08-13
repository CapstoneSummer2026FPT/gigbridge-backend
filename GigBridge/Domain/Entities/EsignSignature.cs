using Domain.Enums.ESign;
using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class EsignSignature
{
    public Guid EsignSignaturesId { get; set; }

    public Guid EsignDocumentsId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Enum ESignerRole: 0=Client, 1=Freelancer
    /// </summary>
    public int SignerRole { get; set; }

    public string SignatureImageUrl { get; set; } = null!;

    public string? IdentityOrTaxCode { get; set; }

    public int? SignatureWidth { get; set; }

    public int? SignatureHeight { get; set; }

    /// <summary>
    /// Enum ESignSignatureStatus: 0=Pending, 1=Signed, 2=Declined
    /// </summary>
    public int Status { get; set; }

    public DateTime? SignedAt { get; set; }

    public DateTime? DeclinedAt { get; set; }

    public string? DeclineReason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? PolicyVersion { get; set; }

    public DateTime? PolicyAcceptedAt { get; set; }

    public DateTime? DraftSubmittedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual EsignDocument EsignDocuments { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
