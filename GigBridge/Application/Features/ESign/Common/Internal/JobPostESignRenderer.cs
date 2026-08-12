using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums.ESign;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ESign.Common.Internal;

internal static class JobPostESignRenderer
{
    public const string TemplateCode = "JOB_POST_CLIENT_COMMITMENT";

    public static async Task<EsignDocument> EnsureDocumentAsync(
        IApplicationDbContext context,
        JobPost jobPost,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await context.Set<EsignDocument>()
            .FirstOrDefaultAsync(
                document =>
                    document.JobPostsId == jobPost.JobPostsId &&
                    document.ContractsId == null,
                cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var template = await FindTemplateAsync(context, cancellationToken);
        var renderedHtml = Render(template, jobPost);

        var document = new EsignDocument
        {
            EsignDocumentsId = Guid.NewGuid(),
            EsignTemplatesId = template.EsignTemplatesId,
            JobPostsId = jobPost.JobPostsId,
            ContractsId = null,
            DocumentCode = $"GB-JOB-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..32].ToUpperInvariant(),
            RenderedHtmlContent = renderedHtml,
            Status = (int)ESignDocumentStatus.PendingSignatures,
            DocumentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(renderedHtml))).ToLowerInvariant(),
            CreatedAt = now
        };

        context.Set<EsignDocument>().Add(document);

        return document;
    }

    private static async Task<EsignTemplate> FindTemplateAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var jobTemplate = await context.Set<EsignTemplate>()
            .Where(template => template.TemplateCode == TemplateCode && template.IsActive)
            .OrderByDescending(template => template.Version)
            .ThenByDescending(template => template.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (jobTemplate is not null)
        {
            return jobTemplate;
        }

        var fallbackTemplate = await context.Set<EsignTemplate>()
            .Where(template => template.TemplateCode == ContractEsignRenderer.FixedPriceTemplateCode && template.IsActive)
            .OrderByDescending(template => template.Version)
            .ThenByDescending(template => template.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return fallbackTemplate
            ?? throw new BadRequestException("Active e-sign template is not configured.");
    }

    private static string Render(EsignTemplate template, JobPost jobPost)
    {
        var templateHtml = template.HtmlContent.Contains("{{Job.", StringComparison.Ordinal)
            ? template.HtmlContent
            : DefaultTemplateHtml();

        var replacements = new Dictionary<string, string?>
        {
            ["{{Job.Title}}"] = jobPost.Title,
            ["{{Job.Description}}"] = jobPost.Description,
            ["{{Job.Budget}}"] = FormatBudget(jobPost),
            ["{{Job.Currency}}"] = jobPost.Currency,
            ["{{Job.EstimatedDuration}}"] = jobPost.EstimatedDuration,
            ["{{Job.Location}}"] = jobPost.Location,
            ["{{Job.EndDate}}"] = jobPost.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        var rendered = templateHtml;
        foreach (var replacement in replacements)
        {
            rendered = rendered.Replace(
                replacement.Key,
                Encode(replacement.Value),
                StringComparison.Ordinal);
        }

        return rendered;
    }

    private static string DefaultTemplateHtml()
    {
        return """
            <section>
              <h1>Job Post E-Sign Commitment</h1>
              <h2>{{Job.Title}}</h2>
              <p>{{Job.Description}}</p>
              <dl>
                <dt>Budget</dt><dd>{{Job.Budget}}</dd>
                <dt>Estimated duration</dt><dd>{{Job.EstimatedDuration}}</dd>
                <dt>Location</dt><dd>{{Job.Location}}</dd>
                <dt>End date</dt><dd>{{Job.EndDate}}</dd>
              </dl>
              <p>By signing, the client confirms this job post is ready to be published.</p>
            </section>
            """;
    }

    private static string FormatBudget(JobPost jobPost)
    {
        var currency = string.IsNullOrWhiteSpace(jobPost.Currency) ? "USD" : jobPost.Currency.Trim();

        return (jobPost.BudgetMin, jobPost.BudgetMax) switch
        {
            ({ } min, { } max) when min == max => FormattableString.Invariant($"{min:0.##} {currency}"),
            ({ } min, { } max) => FormattableString.Invariant($"{min:0.##} - {max:0.##} {currency}"),
            ({ } min, null) => FormattableString.Invariant($"From {min:0.##} {currency}"),
            (null, { } max) => FormattableString.Invariant($"Up to {max:0.##} {currency}"),
            _ => "Not specified"
        };
    }

    private static string Encode(string? value)
    {
        return WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim());
    }
}
