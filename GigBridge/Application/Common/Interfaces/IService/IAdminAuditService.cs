namespace Application.Common.Interfaces.IService;

public interface IAdminAuditService
{
    Guid Add(Guid adminId, string action, string entityType, Guid? entityId, object? oldValues, object? newValues);
}

public interface IRequestMetadataAccessor
{
    Guid CorrelationId { get; }
    string? UserAgent { get; }
}
