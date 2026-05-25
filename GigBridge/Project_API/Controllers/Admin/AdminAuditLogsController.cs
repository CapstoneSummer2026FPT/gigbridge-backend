using Application.DTOs.Admin;
using Application.Common.Models;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/audit-logs")]
public sealed class AdminAuditLogsController : AdminControllerBase
{
    private readonly IAdminAuditLogService _auditLogService;

    public AdminAuditLogsController(IAdminAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] AuditLogPageQueryDto query, CancellationToken cancellationToken)
    {
        var data = await _auditLogService.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Audit logs retrieved successfully"));
    }

    [HttpGet("{auditLogId:guid}")]
    public async Task<IActionResult> Get(Guid auditLogId, CancellationToken cancellationToken)
    {
        var data = await _auditLogService.GetAsync(auditLogId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Audit log retrieved successfully"));
    }
}
