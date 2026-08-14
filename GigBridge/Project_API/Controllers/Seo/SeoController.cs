using Application.Common.Models;
using Application.Features.Seo.PublicMarketplace.DTOs;
using Application.Features.Seo.PublicMarketplace.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Seo;

[ApiController]
[AllowAnonymous]
[Route("api/seo")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SeoController : BaseApiController
{
    [HttpGet("sitemap-resources")]
    public async Task<IActionResult> GetSitemapResources(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSeoSitemapResourcesQuery(), cancellationToken);
        return Ok(ApiResponse<SeoSitemapResourcesDto>.Ok(result, "Success"));
    }
}
