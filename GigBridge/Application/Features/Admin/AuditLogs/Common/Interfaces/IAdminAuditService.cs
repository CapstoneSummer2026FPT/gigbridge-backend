namespace Application.Features.Admin.AuditLogs.Common.Interfaces;

public interface IAdminAuditService
{
    Guid Add(Guid adminId, string action, string entityType, Guid? entityId, object? oldValues, object? newValues);
}
