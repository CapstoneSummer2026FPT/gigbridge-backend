using Application.DTOs.Admin;
using Application.Features.Admin.AuditLogs.Dto;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Admin;

public sealed class AdminAuditLogService : AdminServiceBase, IAdminAuditLogService
{
    private readonly IMapper _mapper;

    public AdminAuditLogService(IApplicationDbContext dbContext, IMapper mapper) : base(dbContext)
    {
        _mapper = mapper;
    }

    public async Task<PagedResultDto<AuditLogDto>> GetAllAsync(AuditLogPageQueryDto query, CancellationToken cancellationToken)
    {
        ValidatePaging(query);
        var logs = DbContext.Set<AdminAuditLog>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            logs = logs.Where(x => x.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            logs = logs.Where(x => x.EntityType == query.EntityType);
        }

        logs = FilterDates(logs, query, x => x.CreatedAt);
        return await ToPageAsync(logs.OrderByDescending(x => x.CreatedAt)
            .ProjectTo<AuditLogDto>(_mapper.ConfigurationProvider), query, cancellationToken);
    }

    public async Task<AuditLogDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await DbContext.Set<AdminAuditLog>().AsNoTracking().Where(x => x.AdminAuditLogsId == id)
            .ProjectTo<AuditLogDto>(_mapper.ConfigurationProvider).SingleOrDefaultAsync(cancellationToken);
        return result ?? throw new NotFoundException(nameof(AdminAuditLog), id);
    }
}

