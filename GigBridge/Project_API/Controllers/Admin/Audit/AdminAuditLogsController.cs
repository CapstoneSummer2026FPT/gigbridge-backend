using Application.Common.Models;
using Application.Features.Admin.AuditLogs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin.Audit;

[ApiController, Route("api/admin/audit-logs"), Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminAuditLogsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetAdminAuditLogsQuery query) =>
        Ok(ApiResponse<PaginatedList<AdminAuditLogDto>>.Ok(await Mediator.Send(query), "Audit logs retrieved successfully."));

    [HttpGet("{auditLogId:guid}")]
    public async Task<IActionResult> GetOne(Guid auditLogId) =>
        Ok(ApiResponse<AdminAuditLogDto>.Ok(await Mediator.Send(new GetAdminAuditLogQuery(auditLogId)), "Audit log retrieved successfully."));
}
