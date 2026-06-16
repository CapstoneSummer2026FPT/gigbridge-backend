using Application.Common.Models;
using Application.Features.Admin.AdminCredit.Commands;
using Application.Features.Admin.AdminCredit.DTOs;
using Application.Features.Wallets.Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/wallets")]
[Authorize(Roles = "Admin")]
public sealed class AdminWalletsController : BaseApiController
{
    [HttpPost("{userId}/credit")]
    public async Task<IActionResult> CreditWallet(
        Guid userId,
        [FromBody] AdminCreditWalletRequest request)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new AdminCreditWalletCommand(adminUserId, userId, request));

        return Ok(ApiResponse<WalletTransactionResponse>.Ok(result, "Wallet credited"));
    }
}
