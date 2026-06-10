using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Features.Profiles.Common.MarkSetupComplete.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers;

[Authorize]
[ApiController]
[Route("api/profile")]
public class ProfileController : BaseApiController
{
    [HttpPut("setup-complete")]
    public async Task<IActionResult> MarkSetupComplete()
    {
        var command = new MarkSetupCompleteCommand();
        await Mediator.Send(command);

        return Ok(ApiResponse<object?>.Ok(null, "Setup marked as complete"));
    }

    [HttpGet("company-sizes")]
    public IActionResult GetCompanySizes()
    {
        var companySizes = new[]
        {
            new { Id = 0, Name = "Solo (1-9 employees)" },
            new { Id = 1, Name = "Small (10-49 employees)" },
            new { Id = 2, Name = "Medium (50-249 employees)" },
            new { Id = 3, Name = "Large (250+ employees)" }
        };
        return Ok(ApiResponse<object>.Ok(companySizes, "Success"));
    }

    [HttpGet("industries")]
    public IActionResult GetIndustries()
    {
        var industries = new[]
        {
            "Technology", "Finance", "Healthcare", "E-commerce", "Education",
            "Marketing", "Real Estate", "Entertainment", "Manufacturing", "Other"
        };
        return Ok(ApiResponse<IEnumerable<string>>.Ok(industries, "Success"));
    }

    [HttpGet("availability-statuses")]
    public IActionResult GetAvailabilityStatuses()
    {
        var availabilityStatuses = new[]
        {
            new { Id = 0, Name = "Available - More than 30 hrs/week" },
            new { Id = 1, Name = "Busy - Less than 30 hrs/week" },
            new { Id = 2, Name = "Not Available" }
        };
        return Ok(ApiResponse<object>.Ok(availabilityStatuses, "Success"));
    }
}


