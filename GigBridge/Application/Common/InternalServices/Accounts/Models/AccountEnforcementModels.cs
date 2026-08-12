using Domain.Enums.Accounts;

namespace Application.Common.InternalServices.Accounts.Models;

public sealed record AccountViolationSource(
    UserViolationSourceType SourceType,
    Guid? DisputeId = null,
    Guid? ReportId = null,
    Guid? ManualActionId = null,
    Guid? ContractId = null,
    Guid? MilestoneId = null);

public sealed record AccountEnforcementResult(
    bool Duplicate,
    int PreviousViolationCount,
    int ViolationCount,
    int PreviousAccountStatus,
    int AccountStatus,
    UserViolationAction? Action,
    DateTime? SuspendedUntil,
    Guid? UserViolationId);
