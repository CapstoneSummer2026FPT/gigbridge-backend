using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Public;

[ApiController]
[Route("api/policies")]
[AllowAnonymous]
public sealed class PoliciesController(IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("gigbridge-vn")]
    public IActionResult GetGigBridgeVietnamPolicy()
    {
        var path = Path.Combine(environment.ContentRootPath, "Policies", "GigBridge_Policy_VN.md");

        return System.IO.File.Exists(path)
            ? PhysicalFile(path, "text/markdown; charset=utf-8")
            : NotFound();
    }
}
