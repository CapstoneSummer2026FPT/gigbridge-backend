using Application.Common.Models;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Users.ClearUserSuspension.Commands;
using Application.Features.Admin.Users.ClearUserSuspension.DTOs;
using Application.Features.Admin.Users.CreateNewUser.Commands;
using Application.Features.Admin.Users.CreateNewUser.DTOs;
using Application.Features.Admin.Users.DeleteUser.Commands;
using Application.Features.Admin.Users.GetAllUser.DTOs;
using Application.Features.Admin.Users.GetAllUser.Queries;
using Application.Features.Admin.Users.GetClientByEmail.Queries;
using Application.Features.Admin.Users.GetFreelancerByEmail.Queries;
using Application.Features.Admin.Users.Shared.DTOs;
using Application.Features.Admin.Users.Premium.Grant.Commands;
using Application.Features.Admin.Users.Premium.Revoke.Commands;
using Application.Features.Admin.Users.SuspendUser.Commands;
using Application.Features.Admin.Users.SuspendUser.DTOs;
using Application.Features.Admin.Users.ToggleUserActivity.Commands;
using Application.Features.Admin.Users.UpdateUser.Commands;
using Application.Features.Admin.Users.UpdateUser.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Features.Admin.Users.Detail;
using Project_API.Controllers.Common;

namespace Project_API.Controllers;

[Route("api/admin/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminUserController : BaseApiController
{
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetDetail(Guid userId)
    {
        if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse();
        return Ok(ApiResponse<AdminUserDetailDto>.Ok(await Mediator.Send(new GetAdminUserDetailQuery(adminId, userId)), "User detail retrieved successfully."));
    }

    [HttpGet("{userId:guid}/violations")]
    public async Task<IActionResult> GetViolations(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); return Ok(ApiResponse<PaginatedList<AdminViolationDto>>.Ok(await Mediator.Send(new GetAdminUserViolationsQuery(adminId, userId, page, pageSize)), "Violations retrieved successfully.")); }

    [HttpGet("{userId:guid}/reports")]
    public async Task<IActionResult> GetReports(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); return Ok(ApiResponse<PaginatedList<AdminUserReportDto>>.Ok(await Mediator.Send(new GetAdminUserReportsQuery(adminId, userId, page, pageSize)), "Reports retrieved successfully.")); }

    [HttpGet("{userId:guid}/audit-logs")]
    public async Task<IActionResult> GetAuditLogs(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); return Ok(ApiResponse<PaginatedList<AdminUserAuditDto>>.Ok(await Mediator.Send(new GetAdminUserAuditLogsQuery(adminId, userId, page, pageSize)), "Audit logs retrieved successfully.")); }

    [HttpPost("{userId:guid}/warning")]
    public Task<IActionResult> Warn(Guid userId, [FromBody] AdminEnforcementRequest request) => Enforce(userId, AccountEnforcementAction.Warning, request);
    [HttpPost("{userId:guid}/suspend")]
    public Task<IActionResult> SuspendById(Guid userId, [FromBody] AdminEnforcementRequest request) => Enforce(userId, AccountEnforcementAction.Suspension, request);
    [HttpPost("{userId:guid}/ban")]
    public Task<IActionResult> Ban(Guid userId, [FromBody] AdminEnforcementRequest request) => Enforce(userId, AccountEnforcementAction.PermanentBan, request);
    [HttpPost("{userId:guid}/clear-suspension")]
    public Task<IActionResult> ClearById(Guid userId, [FromBody] AdminReasonRequest request) => ClearOrRestore(userId, request, false);
    [HttpPost("{userId:guid}/restore")]
    public Task<IActionResult> Restore(Guid userId, [FromBody] AdminReasonRequest request) => ClearOrRestore(userId, request, true);

    private async Task<IActionResult> Enforce(Guid userId, AccountEnforcementAction action, AdminEnforcementRequest request)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); var result = await Mediator.Send(new EnforceAdminUserCommand(adminId, userId, action, request)); return Ok(ApiResponse<AccountEnforcementResult>.Ok(result, "Account enforcement applied successfully.")); }
    private async Task<IActionResult> ClearOrRestore(Guid userId, AdminReasonRequest request, bool restore)
    { if (!TryGetCurrentUserId(out var adminId)) return InvalidTokenResponse(); await Mediator.Send(new ClearAdminUserSuspensionCommand(adminId, userId, request.Reason, restore)); return Ok(ApiResponse<object>.Ok(null!, restore ? "User restored successfully." : "Suspension cleared successfully.")); }
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetAllUsersQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<GetAllUsersResponse>.Ok(result, "Users retrieved successfully"));
    }

    [HttpGet("client-by-email")]
    public async Task<IActionResult> GetClientByEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(ApiResponse<object>.BadRequest("Email is required"));

        var user = await Mediator.Send(new GetClientByEmailQuery(email));

        if (user is null)
            return NotFound(ApiResponse<object>.NotFound("Client not found"));

        return Ok(ApiResponse<AdminUserDto>.Ok(user, "Client retrieved successfully"));
    }

    [HttpGet("freelancer-by-email")]
    public async Task<IActionResult> GetFreelancerByEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(ApiResponse<object>.BadRequest("Email is required"));

        var user = await Mediator.Send(new GetFreelancerByEmailQuery(email));

        if (user is null)
            return NotFound(ApiResponse<object>.NotFound("Freelancer not found"));

        return Ok(ApiResponse<AdminUserDto>.Ok(user, "Freelancer retrieved successfully"));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        if (request is null)
            return BadRequest(ApiResponse<object>.BadRequest("Request body is required"));

        var user = await Mediator.Send(new CreateNewUserCommand(request));
        return Ok(ApiResponse<AdminUserDto>.Ok(user, "User created successfully"));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateUserCommand command)
    {
        if (command is null || command.Request is null)
            return BadRequest(ApiResponse<object>.BadRequest("Request body is required"));

        var user = await Mediator.Send(command);

        if (user is null)
            return NotFound(ApiResponse<object>.NotFound("User not found"));

        return Ok(ApiResponse<AdminUserDto>.Ok(user, "User updated successfully"));
    }

    [HttpPatch("toggle-activity")]
    public async Task<IActionResult> ToggleActivity([FromBody] ToggleUserActivityCommand command)
    {
        var result = await Mediator.Send(command);

        if (!result)
            return NotFound(ApiResponse<object>.NotFound("User not found"));

        return Ok(ApiResponse<object>.NoContent("User activity toggled successfully"));
    }

    [HttpPatch("suspend")]
    public async Task<IActionResult> Suspend([FromBody] SuspendUserRequest request)
    {
        if (request is null)
            return BadRequest(ApiResponse<object>.BadRequest("Request body is required"));

        var user = await Mediator.Send(new SuspendUserCommand(request));

        if (user is null)
            return NotFound(ApiResponse<object>.NotFound("User not found"));

        return Ok(ApiResponse<AdminUserDto>.Ok(user, "User suspended successfully"));
    }

    [HttpPatch("clear-suspension")]
    public async Task<IActionResult> ClearSuspension([FromBody] ClearUserSuspensionRequest request)
    {
        if (request is null)
            return BadRequest(ApiResponse<object>.BadRequest("Request body is required"));

        var user = await Mediator.Send(new ClearUserSuspensionCommand(request));

        if (user is null)
            return NotFound(ApiResponse<object>.NotFound("User not found"));

        return Ok(ApiResponse<AdminUserDto>.Ok(user, "User suspension cleared successfully"));
    }

    [HttpPost("{userId:guid}/premium")]
    public async Task<IActionResult> GrantPremium(Guid userId)
    {
        var changed = await Mediator.Send(new GrantUserPremiumCommand(userId));
        return Ok(ApiResponse<object>.Ok(new { changed }, changed ? "Premium granted" : "User is already Premium"));
    }

    [HttpDelete("{userId:guid}/premium")]
    public async Task<IActionResult> RevokePremium(Guid userId)
    {
        var changed = await Mediator.Send(new RevokeUserPremiumCommand(userId));
        return Ok(ApiResponse<object>.Ok(new { changed }, changed ? "Premium revoked" : "User is not Premium"));
    }

    [HttpDelete]
    public async Task<IActionResult> Delete([FromBody] DeleteUserCommand command)
    {
        var result = await Mediator.Send(command);

        if (!result)
            return NotFound(ApiResponse<object>.NotFound("User not found"));

        return Ok(ApiResponse<object>.NoContent("User deleted successfully"));
    }
}
