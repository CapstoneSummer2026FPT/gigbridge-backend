using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Admin.AuditLogs.Interfaces;
using Application.Common.InternalServices.Admin.AuditLogs.Services;
using Domain.Entities;

namespace Application.Common.InternalServices.Admin.AuditLogs.Services;
public sealed class AdminAuditService : IAdminAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApplicationDbContext _context;
    private readonly IRequestMetadataAccessor _request;
    private readonly IDateTimeService _clock;

    public AdminAuditService(IApplicationDbContext context, IRequestMetadataAccessor request, IDateTimeService clock)
    { _context = context; _request = request; _clock = clock; }

    public Guid Add(Guid adminId, string action, string entityType, Guid? entityId, object? oldValues, object? newValues)
    {
        var id = Guid.NewGuid();
        _context.Set<AdminAuditLog>().Add(new AdminAuditLog
        {
            AdminAuditLogsId = id, AdminId = adminId, Action = action,
            EntityType = entityType, EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, JsonOptions),
            CorrelationId = _request.CorrelationId,
            UserAgent = _request.UserAgent,
            CreatedAt = _clock.UtcNow
        });
        return id;
    }
}
