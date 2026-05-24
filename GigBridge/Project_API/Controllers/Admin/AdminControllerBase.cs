using System.Security.Claims;
using Application.DTOs.Admin;
using Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin;

[Authorize(Roles = "Admin")]
public abstract class AdminControllerBase : ControllerBase
{
    protected AdminActorDto GetActor()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(rawId, out var adminId))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["adminId"] = ["The authenticated administrator token must contain a GUID subject/name identifier for audited actions."]
            });
        }

        return new AdminActorDto
        {
            AdminId = adminId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };
    }
}
