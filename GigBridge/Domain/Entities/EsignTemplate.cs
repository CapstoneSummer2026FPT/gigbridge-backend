using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class EsignTemplate
{
    public Guid EsignTemplatesId { get; set; }

    public string Name { get; set; } = null!;

    public string HtmlContent { get; set; } = null!;

    public int Version { get; set; }

    public string? PlaceholderSchema { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<EsignDocument> EsignDocuments { get; set; } = new List<EsignDocument>();
}
