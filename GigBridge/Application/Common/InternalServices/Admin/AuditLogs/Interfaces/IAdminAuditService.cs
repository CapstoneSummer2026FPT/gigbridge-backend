namespace Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
public interface IAdminAuditService
{
    Guid Add(Guid adminId, string action, string entityType, Guid? entityId, object? oldValues, object? newValues);
}
