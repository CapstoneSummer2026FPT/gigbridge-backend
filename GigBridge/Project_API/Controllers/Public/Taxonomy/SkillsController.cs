using Application.Common.Models;
using Application.Features.Skills.Common.DTOs;
using Application.Features.Skills.Public.GetByCategory.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Public.Taxonomy;

[ApiController]
[Route("api/Skills")]
[AllowAnonymous]
public sealed class SkillsController : BaseApiController
{
    [HttpGet("by-category/{categoryId:guid}")]
    public async Task<IActionResult> GetSkillsByCategory(Guid categoryId)
    {
        var result = await Mediator.Send(new GetSkillsByCategoryQuery(categoryId));
        return Ok(ApiResponse<IReadOnlyList<SkillOptionDto>>.Ok(result, "Skills retrieved successfully"));
    }
}
