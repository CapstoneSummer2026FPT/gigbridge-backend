using Application.Common.Models;
using Application.Features.Admin.Assets.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/assets")]
[Authorize(Roles = "Admin")]
public sealed class AdminAssetsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAssets(
        [FromQuery] string? search,
        [FromQuery] Guid? jobPostId,
        [FromQuery] Guid? uploadedByUserId)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetAdminAssetsQuery(adminUserId, search, jobPostId, uploadedByUserId));
        return Ok(ApiResponse<IReadOnlyList<AdminAssetDto>>.Ok(result, "Success"));
    }
}
