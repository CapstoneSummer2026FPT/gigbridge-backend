using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.Admin.AuditLogs;

public sealed record AdminAuditLogDto(Guid AuditLogId, Guid AdminUserId, string AdminName, string? AdminAvatar, string Action,
    string? EntityType, Guid? EntityId, JsonElement? OldValues, JsonElement? NewValues,
    Guid CorrelationId, string? UserAgent, DateTime CreatedAt);

public sealed record GetAdminAuditLogsQuery(int Page = 1, int PageSize = 20, Guid? AdminId = null,
    string? Action = null, string? EntityType = null, Guid? EntityId = null,
    DateTime? From = null, DateTime? To = null, string? Search = null,
    Guid? CorrelationId = null) : IRequest<PaginatedList<AdminAuditLogDto>>;
public sealed record GetAdminAuditLogQuery(Guid AuditLogId) : IRequest<AdminAuditLogDto>;

public sealed class AdminAuditLogQueryHandler :
    IRequestHandler<GetAdminAuditLogsQuery, PaginatedList<AdminAuditLogDto>>,
    IRequestHandler<GetAdminAuditLogQuery, AdminAuditLogDto>
{
    private readonly IApplicationDbContext _context;
    public AdminAuditLogQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedList<AdminAuditLogDto>> Handle(GetAdminAuditLogsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page); var size = Math.Clamp(request.PageSize, 1, PaginatedQuery.MaxPageSize);
        var query = _context.Set<AdminAuditLog>().AsNoTracking().Include(x => x.Admin).AsQueryable();
        if (request.AdminId.HasValue) query = query.Where(x => x.AdminId == request.AdminId);
        if (!string.IsNullOrWhiteSpace(request.Action)) query = query.Where(x => x.Action == request.Action);
        if (!string.IsNullOrWhiteSpace(request.EntityType)) query = query.Where(x => x.EntityType == request.EntityType);
        if (request.EntityId.HasValue) query = query.Where(x => x.EntityId == request.EntityId);
        if (request.CorrelationId.HasValue) query = query.Where(x => x.CorrelationId == request.CorrelationId);
        if (request.From.HasValue) query = query.Where(x => x.CreatedAt >= request.From);
        if (request.To.HasValue) query = query.Where(x => x.CreatedAt <= request.To);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(x => x.Action.ToLower().Contains(term) ||
                (x.OldValues != null && x.OldValues.ToLower().Contains(term)) ||
                (x.NewValues != null && x.NewValues.ToLower().Contains(term)));
        }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PaginatedList<AdminAuditLogDto>(rows.Select(Map).ToList(), count, page, size);
    }

    public async Task<AdminAuditLogDto> Handle(GetAdminAuditLogQuery request, CancellationToken ct)
    {
        var row = await _context.Set<AdminAuditLog>().AsNoTracking().Include(x => x.Admin)
            .FirstOrDefaultAsync(x => x.AdminAuditLogsId == request.AuditLogId, ct)
            ?? throw new NotFoundException("Audit log does not exist.");
        return Map(row);
    }
    private static AdminAuditLogDto Map(AdminAuditLog x) => new(x.AdminAuditLogsId, x.AdminId,
        x.Admin.FullName, x.Admin.Avatar, x.Action, x.EntityType, x.EntityId, Parse(x.OldValues), Parse(x.NewValues),
        x.CorrelationId, x.UserAgent, x.CreatedAt);
    private static JsonElement? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
