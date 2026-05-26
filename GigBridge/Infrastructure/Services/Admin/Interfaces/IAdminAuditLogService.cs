using Application.DTOs.Admin;
using Application.Features.Admin.AuditLogs.Dto;

namespace Infrastructure.Services.Admin.Interfaces;

public interface IAdminAuditLogService
{
    Task<PagedResultDto<AuditLogDto>> GetAllAsync(AuditLogPageQueryDto query, CancellationToken cancellationToken);
    Task<AuditLogDto> GetAsync(Guid id, CancellationToken cancellationToken);
}

