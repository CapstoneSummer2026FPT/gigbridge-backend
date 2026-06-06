using System.Security.Claims;
using Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
namespace Project_API.Controllers.Common;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase {
    private IMediator? _mediator;
    protected IMediator Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<IMediator>();

    protected bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdValue, out userId);
    }

    protected IActionResult InvalidTokenResponse()
    {
        return Unauthorized(ApiResponse<object>.Error(401, "Invalid token"));
    }
}
