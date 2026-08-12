using Application.Common.Models;
using Application.Features.Admin.Reconciliation.Common.DTOs;
using Application.Features.Admin.Reconciliation.Queries;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin.Reconciliation;

[Route("api/admin/reconciliation")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminReconciliationController : BaseApiController
{
    /// <summary>
    /// Read-only financial reconciliation report over the contract economy. Surfaces
    /// existing deflated escrows from the legacy G-coin/VND unit bug and any wallet or
    /// milestone-plan drift. Never writes to wallet, ledger, escrow, or contract tables.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetReconciliationReport()
    {
        var result = await Mediator.Send(new GetEscrowReconciliationReportQuery());
        return Ok(ApiResponse<EscrowReconciliationReport>.Ok(result, "Reconciliation report generated successfully"));
    }
}
