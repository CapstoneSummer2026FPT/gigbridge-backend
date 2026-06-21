using Application.Common.Models;
using Application.Features.Majors.Common.DTOs;
using Application.Features.Majors.Public.GetAll.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Public.Taxonomy;

[ApiController]
[Route("api/Majors")]
[AllowAnonymous]
public sealed class MajorsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetMajors()
    {
        var result = await Mediator.Send(new GetAllMajorsQuery());
        return Ok(ApiResponse<IReadOnlyList<MajorDto>>.Ok(result, "Majors retrieved successfully"));
    }
}
