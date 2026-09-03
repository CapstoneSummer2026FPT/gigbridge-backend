using Application.Common.Interfaces;
using Application.Common.Interfaces.Email;
using Application.Common.InternalServices.Contracts.Interfaces;
using Application.Common.InternalServices.Contracts.Models;
using Application.Common.Models.Email;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Features.Contracts.Common.Internal;

/// <summary>
/// Emails the client when the freelancer sends the plan back for rework. Shared by both freelancer
/// review gates so the two produce the same message. Delivery failures are logged and swallowed:
/// the plan change itself is already committed, and losing the email must not fail the request.
/// </summary>
internal static class ContractPlanChangeEmails
{
    public static async Task SendToClientAsync(
        IApplicationDbContext context,
        IEmailService emailService,
        IContractPlanChangeEmailRenderer renderer,
        IConfiguration configuration,
        ILogger logger,
        Contract contract,
        Guid clientUserId,
        Guid freelancerUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var users = await context.Set<User>()
                .AsNoTracking()
                .Where(user => user.UserId == clientUserId || user.UserId == freelancerUserId)
                .Select(user => new { user.UserId, user.FullName, user.Email })
                .ToListAsync(cancellationToken);
            var client = users.FirstOrDefault(user => user.UserId == clientUserId);
            var freelancer = users.FirstOrDefault(user => user.UserId == freelancerUserId);

            if (client is null || string.IsNullOrWhiteSpace(client.Email))
            {
                logger.LogWarning(
                    "Could not send contract plan change email because client {ClientUserId} has no email.",
                    clientUserId);
                return;
            }

            var frontendBaseUrl = (configuration["FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
            var renderedEmail = renderer.Render(new ContractPlanChangeEmailModel(
                client.FullName,
                freelancer?.FullName ?? "Freelancer",
                contract.Title,
                reason,
                $"{frontendBaseUrl}/contracts/{contract.ContractsId}"));

            await emailService.SendEmailAsync(new EmailRequest
            {
                To = client.Email,
                Subject = renderedEmail.Subject,
                Body = renderedEmail.HtmlBody,
                TextBody = renderedEmail.TextBody,
                IsHtml = true,
                IdempotencyKey = $"contract-plan-change:{contract.ContractsId:N}:{contract.RevisionNumber}"
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Failed to send contract plan change email to client {ClientUserId} for contract {ContractId}.",
                clientUserId,
                contract.ContractsId);
        }
    }
}
