namespace Application.Common.InternalServices.Admin.AuditLogs.Services;
public static class AdminAuditActions
{
    public const string UserCreated = "AdminUserCreated";
    public const string UserUpdated = "AdminUserUpdated";
    public const string UserActivated = "AdminUserActivated";
    public const string UserDeactivated = "AdminUserDeactivated";
    public const string WarningIssued = "AdminUserWarningIssued";
    public const string UserSuspended = "AdminUserSuspended";
    public const string SuspensionCleared = "AdminUserSuspensionCleared";
    public const string UserBanned = "AdminUserBanned";
    public const string UserRestored = "AdminUserRestored";
    public const string AccountReportReviewing = "AccountReportMarkedUnderReview";
    public const string AccountReportDismissed = "AccountReportDismissed";
    public const string AccountReportResolved = "AccountReportResolved";
    public const string AccountReportWarning = "AccountReportResolvedWithWarning";
    public const string AccountReportSuspension = "AccountReportResolvedWithSuspension";
    public const string AccountReportBan = "AccountReportResolvedWithBan";
    public const string AccountReportEvidenceDownloaded = "AccountReportEvidenceDownloaded";
    public const string ContractReportAssigned = "ContractReportAssigned";
    public const string ContractReportReassigned = "ContractReportReassigned";
    public const string ContractReportInformationRequested = "ContractReportInformationRequested";
    public const string ContractReportInternalNoteAdded = "ContractReportInternalNoteAdded";
    public const string ContractReportDismissed = "ContractReportDismissed";
    public const string ContractReportClosed = "ContractReportClosed";
    public const string ContractReportEscalated = "ContractReportEscalated";
    public const string ContractReportLinkedToDispute = "ContractReportLinkedToDispute";
    public const string ContractReportEvidenceDownloaded = "ContractReportEvidenceDownloaded";
    public const string ContractReportInvestigationViewed = "ContractReportInvestigationViewed";
    public const string ProposalInvalidated = "ProposalInvalidated";
    public const string ProposalRestored = "ProposalRestored";
    public const string ProposalInternalNoteAdded = "ProposalInternalNoteAdded";
}
