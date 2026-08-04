using Application.Common.Models;
using Application.Features.MajorCategories.Common.DTOs;
using Application.Features.MajorCategories.Public.GetAll.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Public.Taxonomy;

[ApiController]
[Route("api/MajorCategories")]
[AllowAnonymous]
public sealed class MajorCategoriesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetMajorCategories()
    {
        var result = await Mediator.Send(new GetAllMajorCategoriesQuery());
        return Ok(ApiResponse<IReadOnlyList<MajorCategoryDto>>.Ok(result, "Major categories retrieved successfully"));
    }
}
