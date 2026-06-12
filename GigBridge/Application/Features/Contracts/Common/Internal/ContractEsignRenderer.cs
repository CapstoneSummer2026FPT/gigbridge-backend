using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Application.Features.Contracts.Common.Internal;

internal static class ContractEsignRenderer
{
    public const string FixedPriceTemplateCode = "CONTRACT_FIXED_PRICE";

    public static async Task<EsignDocument> EnsureDocumentAsync(
        IApplicationDbContext context,
        Contract contract,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await context.Set<EsignDocument>()
            .FirstOrDefaultAsync(document => document.ContractsId == contract.ContractsId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var template = await context.Set<EsignTemplate>()
            .Where(template => template.TemplateCode == FixedPriceTemplateCode && template.IsActive)
            .OrderByDescending(template => template.Version)
            .ThenByDescending(template => template.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
        {
            throw new BadRequestException("Active e-sign contract template CONTRACT_FIXED_PRICE is not configured.");
        }

        var milestones = await context.Set<Milestone>()
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .OrderBy(milestone => milestone.SortOrder)
            .ThenBy(milestone => milestone.CreatedAt)
            .ToListAsync(cancellationToken);

        var clientProfile = await context.Set<ClientProfile>()
            .FirstOrDefaultAsync(
                profile => profile.ClientProfilesId == contract.ClientProfilesId,
                cancellationToken);

        var clientUser = clientProfile is null
            ? null
            : await context.Set<User>()
                .FirstOrDefaultAsync(user => user.UserId == clientProfile.UserId, cancellationToken);

        User? freelancerUser = null;
        if (contract.FreelancerProfilesId.HasValue)
        {
            var freelancerProfile = await context.Set<FreelancerProfile>()
                .FirstOrDefaultAsync(
                    profile => profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                    cancellationToken);

            if (freelancerProfile is not null)
            {
                freelancerUser = await context.Set<User>()
                    .FirstOrDefaultAsync(user => user.UserId == freelancerProfile.UserId, cancellationToken);
            }
        }

        var renderedHtml = Render(template.HtmlContent, contract, milestones, clientUser, freelancerUser);
        var document = new EsignDocument
        {
            EsignDocumentsId = Guid.NewGuid(),
            EsignTemplatesId = template.EsignTemplatesId,
            JobPostsId = contract.JobPostsId,
            ContractsId = contract.ContractsId,
            DocumentCode = $"GB-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..22].ToUpperInvariant(),
            RenderedHtmlContent = renderedHtml,
            Status = (int)ESignDocumentStatus.PendingSignatures,
            DocumentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(renderedHtml))).ToLowerInvariant(),
            CreatedAt = now
        };

        context.Set<EsignDocument>().Add(document);

        return document;
    }

    private static string Render(
        string template,
        Contract contract,
        IReadOnlyList<Milestone> milestones,
        User? clientUser,
        User? freelancerUser)
    {
        var milestoneRows = string.Join(
            string.Empty,
            milestones.Select((milestone, index) =>
            {
                var dueDate = milestone.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty;
                return "<tr>" +
                    $"<td>{index + 1}</td>" +
                    $"<td>{Encode(milestone.Title)}</td>" +
                    $"<td>{milestone.Amount:0.##} VND</td>" +
                    $"<td>{Encode(dueDate)}</td>" +
                    "</tr>";
            }));

        var replacements = new Dictionary<string, (string? Value, bool IsHtml)>
        {
            ["{{Contract.Title}}"] = (contract.Title, false),
            ["{{Contract.Description}}"] = (contract.Description, false),
            ["{{Contract.TotalBudget}}"] = ($"{contract.TotalBudget:0.##} VND", false),
            ["{{Contract.StartDate}}"] = (contract.StartDate?.ToString("yyyy-MM-dd"), false),
            ["{{Contract.EndDate}}"] = (contract.EndDate?.ToString("yyyy-MM-dd"), false),
            ["{{Contract.DisputeTerms}}"] = (contract.DisputeTerms, false),
            ["{{Client.Name}}"] = (clientUser?.FullName, false),
            ["{{Client.Email}}"] = (clientUser?.Email, false),
            ["{{Freelancer.Name}}"] = (freelancerUser?.FullName, false),
            ["{{Freelancer.Email}}"] = (freelancerUser?.Email, false),
            ["{{MilestonesHtml}}"] = (milestoneRows, true)
        };

        var rendered = template;
        foreach (var replacement in replacements)
        {
            rendered = rendered.Replace(
                replacement.Key,
                replacement.Value.IsHtml ? replacement.Value.Value ?? string.Empty : Encode(replacement.Value.Value));
        }

        return rendered;
    }

    private static string Encode(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
