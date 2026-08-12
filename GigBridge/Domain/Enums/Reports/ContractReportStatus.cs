namespace Domain.Enums.Reports;

public enum ContractReportStatus
{
    Pending = 0,
    WaitingReporterConfirmation = 1,
    Resolved = 2,
    Escalated = 3
}
