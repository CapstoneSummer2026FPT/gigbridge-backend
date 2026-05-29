using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class User
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? Avatar { get; set; }

    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Enum UserRole: 0=Client, 1=Freelancer, 2=Admin
    /// </summary>
    public int Role { get; set; }

    public bool IsEmailVerified { get; set; }

    public bool IsActive { get; set; }

    public string? PreferredLanguage { get; set; }

    public string? Provider { get; set; }

    public string? ProviderId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? EmailVerificationToken { get; set; }

    public DateTime? TokenExpiry { get; set; }
    public string? RefreshTokenHash { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }


    public virtual ICollection<AdminAuditLog> AdminAuditLogs { get; set; } = new List<AdminAuditLog>();

    public virtual ClientProfile? ClientProfile { get; set; }

    public virtual ICollection<Conversation> ConversationUser1s { get; set; } = new List<Conversation>();

    public virtual ICollection<Conversation> ConversationUser2s { get; set; } = new List<Conversation>();

    public virtual ICollection<DisputeEvidence> DisputeEvidences { get; set; } = new List<DisputeEvidence>();

    public virtual ICollection<DisputeMessage> DisputeMessages { get; set; } = new List<DisputeMessage>();

    public virtual ICollection<Dispute> DisputeResolvedByAdmins { get; set; } = new List<Dispute>();

    public virtual ICollection<Dispute> DisputeInitiators { get; set; } = new List<Dispute>();

    public virtual ICollection<EsignSignature> EsignSignatures { get; set; } = new List<EsignSignature>();

    public virtual ICollection<EsignTemplate> EsignTemplates { get; set; } = new List<EsignTemplate>();

    public virtual FreelancerProfile? FreelancerProfile { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<MilestoneAttachment> MilestoneAttachments { get; set; } = new List<MilestoneAttachment>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<PaymentProof> PaymentProofs { get; set; } = new List<PaymentProof>();

    public virtual ICollection<PlatformSetting> PlatformSettings { get; set; } = new List<PlatformSetting>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<Report> ReportResolvedByAdmins { get; set; } = new List<Report>();

    public virtual ICollection<Report> ReportReporters { get; set; } = new List<Report>();

    public virtual ICollection<Review> ReviewReviewees { get; set; } = new List<Review>();

    public virtual ICollection<Review> ReviewReviewers { get; set; } = new List<Review>();

    public virtual ICollection<SavedFreelancer> SavedFreelancers { get; set; } = new List<SavedFreelancer>();

    public virtual ICollection<SavedJob> SavedJobs { get; set; } = new List<SavedJob>();

    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}