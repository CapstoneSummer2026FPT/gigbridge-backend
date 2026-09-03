using Application.Common.Models;
using Application.Features.Categories.Common.DTOs;
using Application.Features.Categories.Public.GetAll.Queries;
using Application.Features.Categories.Public.GetByMajor.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Public.Taxonomy;

[ApiController]
[Route("api/Categories")]
[AllowAnonymous]
public sealed class CategoriesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var result = await Mediator.Send(new GetAllCategoriesQuery());
        return Ok(ApiResponse<IReadOnlyList<CategoryDto>>.Ok(result, "Categories retrieved successfully"));
    }

    [HttpGet("by-major/{majorId:guid}")]
    public async Task<IActionResult> GetCategoriesByMajor(Guid majorId)
    {
        var result = await Mediator.Send(new GetCategoriesByMajorQuery(majorId));
        return Ok(ApiResponse<IReadOnlyList<CategoryOptionDto>>.Ok(result, "Categories retrieved successfully"));
    }
}
