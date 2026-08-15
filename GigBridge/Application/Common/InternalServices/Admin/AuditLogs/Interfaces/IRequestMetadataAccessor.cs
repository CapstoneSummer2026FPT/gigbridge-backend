namespace Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
public interface IRequestMetadataAccessor
{
    Guid CorrelationId { get; }
    string? UserAgent { get; }
}
