namespace Application.Features.Admin.AuditLogs.Common.Interfaces;

public interface IRequestMetadataAccessor
{
    Guid CorrelationId { get; }
    string? UserAgent { get; }
}
