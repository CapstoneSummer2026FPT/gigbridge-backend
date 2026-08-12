using Domain.Enums.Reports;
using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Report
{
    public Guid ReportsId { get; set; }

    public Guid ReporterId { get; set; }

    public Guid ReportedEntityId { get; set; }

    public string ReportedEntityType { get; set; } = null!;

    /// <summary>
    /// Enum ReportType: 0=Spam, 1=Fraud, 2=InappropriateContent, 3=HarassmentOrAbuse, 4=Other, 5=PaymentDispute
    /// </summary>
    public int Type { get; set; }

    public string Reason { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// Enum ReportStatus: 0=Pending, 1=Reviewing, 2=Resolved, 3=Dismissed
    /// </summary>
    public int Status { get; set; }

    public string? AdminNote { get; set; }

    public int? ResolutionAction { get; set; }

    public Guid? AssignedAdminId { get; set; }

    public DateTime? AssignedAt { get; set; }

    public Guid? ResolvedByAdminId { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User? ResolvedByAdmin { get; set; }

    public virtual User? AssignedAdmin { get; set; }

    public virtual User Reporter { get; set; } = null!;

    public virtual ICollection<ReportEvidence> ReportEvidences { get; set; } = new List<ReportEvidence>();

    public virtual ICollection<UserViolation> UserViolations { get; set; } = new List<UserViolation>();
}
