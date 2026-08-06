using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

public partial class User
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Password { get; set; }

    public string? Avatar { get; set; }

    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Enum UserRole: 0=Client, 1=Freelancer, 2=Admin
    /// </summary>
    public int Role { get; set; }

    public bool IsEmailVerified { get; set; }

    public bool IsActive { get; set; }

    public bool IsSetup { get; set; }

    [NotMapped]
    public bool IsPremium
    {
        get
        {
            var now = DateTime.UtcNow;

            return (Role == (int)UserRole.Client || Role == (int)UserRole.Freelancer) &&
                Subscriptions.Any(subscription =>
                    subscription.Status == SubscriptionStatus.Active &&
                    subscription.StartDate <= now &&
                    subscription.EndDate > now &&
                    subscription.SubscriptionPlans is
                    {
                        IsActive: true,
                        Price: > 0
                    } plan &&
                    (plan.TargetRole == null || plan.TargetRole == Role));
        }
    }

    public DateTime? SuspendedUntil { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public string? SuspensionReason { get; set; }

    public int ViolationCount { get; set; }

    public bool IsFlagged { get; set; }

    public int AccountStatus { get; set; }

    public DateTime? BannedAt { get; set; }

    public string? BanReason { get; set; }

    public string? PreferredLanguage { get; set; }

    public string? Provider { get; set; }

    public string? ProviderId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? RefreshTokenHash { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }


    public virtual ICollection<AdminAuditLog> AdminAuditLogs { get; set; } = new List<AdminAuditLog>();

    public virtual ClientProfile? ClientProfile { get; set; }

    public virtual ICollection<Conversation> CreatedConversations { get; set; } = new List<Conversation>();

    public virtual ICollection<ConversationParticipant> ConversationParticipants { get; set; } = new List<ConversationParticipant>();

    public virtual ICollection<ContractProductHandoff> SubmittedContractProductHandoffs { get; set; } = new List<ContractProductHandoff>();

    public virtual ICollection<ContractProductHandoff> ReceivedContractProductHandoffs { get; set; } = new List<ContractProductHandoff>();

    public virtual ICollection<DisputeEvidence> DisputeEvidences { get; set; } = new List<DisputeEvidence>();

    public virtual ICollection<Dispute> DisputeResolvedByAdmins { get; set; } = new List<Dispute>();

    public virtual ICollection<Dispute> DisputeInitiators { get; set; } = new List<Dispute>();

    public virtual ICollection<Dispute> DisputeRespondents { get; set; } = new List<Dispute>();

    public virtual ICollection<Dispute> DisputeAssignedByAdmins { get; set; } = new List<Dispute>();

    public virtual ICollection<EsignSignature> EsignSignatures { get; set; } = new List<EsignSignature>();

    public virtual ICollection<EsignTemplate> EsignTemplates { get; set; } = new List<EsignTemplate>();

    public virtual FreelancerProfile? FreelancerProfile { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<MilestoneAttachment> MilestoneAttachments { get; set; } = new List<MilestoneAttachment>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Schedule> CreatedSchedules { get; set; } = new List<Schedule>();

    public virtual ICollection<Schedule> CancelledSchedules { get; set; } = new List<Schedule>();

    public virtual ICollection<BroadcastNotificationRecipient> BroadcastNotificationRecipients { get; set; } = new List<BroadcastNotificationRecipient>();

    public virtual ICollection<PlatformSetting> PlatformSettings { get; set; } = new List<PlatformSetting>();

    public virtual ICollection<ProposalQuestionTimer> ProposalQuestionTimers { get; set; } = new List<ProposalQuestionTimer>();

    public virtual ICollection<ProposalInterviewReviewSession> ProposalInterviewReviewSessions { get; set; } = new List<ProposalInterviewReviewSession>();

    public virtual ICollection<Report> ReportResolvedByAdmins { get; set; } = new List<Report>();

    public virtual ICollection<Report> ReportAssignedAdmins { get; set; } = new List<Report>();

    public virtual ICollection<Report> ReportReporters { get; set; } = new List<Report>();

    public virtual ICollection<ReportContract> ReportContractReporters { get; set; } = new List<ReportContract>();

    public virtual ICollection<ReportContract> ReportContractRespondents { get; set; } = new List<ReportContract>();

    public virtual ICollection<ReportContract> ReportContractResolvedBy { get; set; } = new List<ReportContract>();

    public virtual ICollection<ReportContract> AssignedReportContracts { get; set; } = new List<ReportContract>();

    public virtual ICollection<ReportContractAdminNote> ReportContractAdminNotes { get; set; } = new List<ReportContractAdminNote>();

    public virtual ICollection<ReportContractInformationRequest> RequestedReportContractInformation { get; set; } = new List<ReportContractInformationRequest>();

    public virtual ICollection<ReportContractInformationRequest> TargetedReportContractInformation { get; set; } = new List<ReportContractInformationRequest>();

    public virtual ICollection<Review> ReviewReviewees { get; set; } = new List<Review>();

    public virtual ICollection<Review> ReviewReviewers { get; set; } = new List<Review>();

    public virtual ICollection<SavedFreelancer> SavedFreelancers { get; set; } = new List<SavedFreelancer>();

    public virtual ICollection<SavedJob> SavedJobs { get; set; } = new List<SavedJob>();

    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    public virtual UserEloScore? UserEloScore { get; set; }

    public virtual ICollection<UserEloPointTransaction> UserEloPointTransactions { get; set; } = new List<UserEloPointTransaction>();

    public virtual ICollection<EloPointAppeal> EloPointAppeals { get; set; } = new List<EloPointAppeal>();

    public virtual UserWallet? UserWallet { get; set; }

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();

    public virtual ICollection<UserViolation> UserViolations { get; set; } = new List<UserViolation>();

    public virtual ICollection<ReportEvidence> UploadedReportEvidences { get; set; } = new List<ReportEvidence>();

    public virtual ICollection<Proposal> InvalidatedProposals { get; set; } = new List<Proposal>();

    public virtual ICollection<ProposalAdminNote> ProposalAdminNotes { get; set; } = new List<ProposalAdminNote>();

    public virtual ICollection<GoogleMeetConnection> GoogleMeetConnections { get; set; } = new List<GoogleMeetConnection>();
    public virtual ICollection<GoogleMeetOAuthState> GoogleMeetOAuthStates { get; set; } = new List<GoogleMeetOAuthState>();
}
