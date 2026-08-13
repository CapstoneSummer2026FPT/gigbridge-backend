using Application.Common.Interfaces;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.Interfaces.Time;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Auditing;

namespace Application.Common.InternalServices.Auditing.Services;

public sealed class UserAuditLogService : IUserAuditLogService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;

    public UserAuditLogService(IApplicationDbContext context, IDateTimeService clock)
    {
        _context = context;
        _clock = clock;
    }

    public void Add(
        Guid userId,
        UserRole role,
        AuditUserActionType actionType,
        Guid contractId,
        string description,
        Guid? jobPostId = null,
        Guid? milestoneId = null,
        Guid? reportId = null,
        Guid? disputeId = null,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        _context.Set<AuditLogWorkSpace>().Add(new AuditLogWorkSpace
        {
            AuditLogWorkSpaceId = Guid.NewGuid(),
            UserId = userId,
            UserRole = (int)role,
            ActionType = (int)actionType,
            ContractId = contractId,
            JobPostId = jobPostId,
            MilestoneId = milestoneId,
            ReportId = reportId,
            DisputeId = disputeId,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            Description = description,
            CreatedAt = _clock.UtcNow
        });
    }
}
