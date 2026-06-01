using Application.Common.Models;
using Application.Features.Proposals.Admin.GetAllProposals.Queries;
using Application.Features.Proposals.Common.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/Proposals")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminProposalsController : BaseApiController
{
    [HttpGet("admin/all")]
    public async Task<IActionResult> GetAllProposals([FromQuery] int pageIndex = 1, int pageSize = 10)
    {
        var query = new GetAllProposalsQuery
        {
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<ProposalDto>>.Ok(result, "Success"));
    }
}
