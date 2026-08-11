using Application.Common.Models;
using Application.Features.Admin.AuditLogs.Users;
using Application.Features.Admin.Disputes.Common.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin.Audit;

[ApiController, Route("api/admin/contracts/{contractId:guid}/audit-logs"), Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminContractUserAuditLogsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid contractId) =>
        Ok(ApiResponse<IReadOnlyList<AdminUserAuditEventResponse>>.Ok(
            await Mediator.Send(new GetContractUserAuditLogsQuery(contractId)),
            "Contract audit logs retrieved successfully."));
}
