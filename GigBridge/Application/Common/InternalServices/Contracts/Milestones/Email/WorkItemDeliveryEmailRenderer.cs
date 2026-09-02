using System.Net;
using System.Text;
using Application.Common.Interfaces.Templates;
using Application.Common.InternalServices.Contracts.Interfaces;
using Application.Common.InternalServices.Contracts.Models;

namespace Application.Common.InternalServices.Contracts.Milestones.Email;

/// <summary>
/// Builds the work item delivery emails on the shared notification layout rather than on three
/// bespoke templates, the same way <c>ScheduleEmailRenderer</c> does. Every interpolated value is
/// HTML-encoded on the way in — work item titles and revision reasons are user-authored text.
/// </summary>
public sealed class WorkItemDeliveryEmailRenderer : IWorkItemDeliveryEmailRenderer
{
    private const string LayoutTemplate = "Common/Email/NotificationLayout.html";

    private readonly ITemplateReader _templateReader;

    public WorkItemDeliveryEmailRenderer(ITemplateReader templateReader)
    {
        _templateReader = templateReader;
    }

    public RenderedDeliveryEmail RenderSubmission(WorkItemSubmissionEmailModel model)
    {
        var headline = model.WorkItemTitles.Count == 1
            ? "A deliverable is ready for review"
            : $"{model.WorkItemTitles.Count} deliverables are ready for review";

        return Render(
            subject: $"Ready for review – {model.MilestoneTitle}",
            badge: "Submitted",
            accent: "#2563eb",
            badgeBackground: "#eff6ff",
            headline: headline,
            recipientName: model.RecipientName,
            introduction: $"Work has been submitted for the milestone {model.MilestoneTitle}.",
            title: model.MilestoneTitle,
            workItemTitles: model.WorkItemTitles,
            workItemsLabel: "Submitted work items",
            reasonLabel: null,
            reason: null,
            actionLabel: "Review deliverables",
            actionUrl: model.ActionUrl);
    }

    public RenderedDeliveryEmail RenderRevisionRequested(WorkItemRevisionEmailModel model)
    {
        var headline = model.WorkItemTitles.Count == 1
            ? "A work item needs changes"
            : $"{model.WorkItemTitles.Count} work items need changes";

        return Render(
            subject: $"Changes requested – {model.MilestoneTitle}",
            badge: "Revision requested",
            accent: "#d97706",
            badgeBackground: "#fffbeb",
            headline: headline,
            recipientName: model.RecipientName,
            introduction: $"The client asked for changes on the milestone {model.MilestoneTitle}.",
            title: model.MilestoneTitle,
            workItemTitles: model.WorkItemTitles,
            workItemsLabel: "Work items to revise",
            reasonLabel: "Reason",
            reason: model.Reason,
            actionLabel: "Open the delivery space",
            actionUrl: model.ActionUrl);
    }

    public RenderedDeliveryEmail RenderMilestoneCompleted(MilestoneAutoCompletedEmailModel model)
    {
        var introduction = model.NextMilestoneTitle is null
            ? $"Every work item in {model.MilestoneTitle} has been approved."
            : $"Every work item in {model.MilestoneTitle} has been approved. Next up: {model.NextMilestoneTitle}.";

        return Render(
            subject: $"Milestone completed – {model.MilestoneTitle}",
            badge: "Completed",
            accent: "#059669",
            badgeBackground: "#ecfdf5",
            headline: "Milestone completed",
            recipientName: model.RecipientName,
            introduction: introduction,
            title: model.MilestoneTitle,
            workItemTitles: [],
            workItemsLabel: null,
            reasonLabel: null,
            reason: null,
            actionLabel: "Open the delivery space",
            actionUrl: model.ActionUrl);
    }

    private RenderedDeliveryEmail Render(
        string subject,
        string badge,
        string accent,
        string badgeBackground,
        string headline,
        string recipientName,
        string introduction,
        string title,
        IReadOnlyList<string> workItemTitles,
        string? workItemsLabel,
        string? reasonLabel,
        string? reason,
        string actionLabel,
        string actionUrl)
    {
        var greeting = string.IsNullOrWhiteSpace(recipientName) ? "Hello," : $"Hello {E(recipientName)},";

        var html = _templateReader.ReadText(LayoutTemplate)
            .Replace("{{PREVIEW}}", E($"{headline}. {introduction}"))
            .Replace("{{BADGE_BACKGROUND}}", badgeBackground)
            .Replace("{{ACCENT}}", accent)
            .Replace("{{BADGE}}", E(badge))
            .Replace("{{HEADLINE}}", E(headline))
            .Replace("{{GREETING}}", greeting)
            .Replace("{{INTRODUCTION}}", E(introduction))
            .Replace("{{TITLE}}", E(title))
            .Replace("{{FORMATTED_TIME}}", string.Empty)
            .Replace("{{ACTOR_LABEL}}", string.Empty)
            .Replace("{{ACTOR_NAME}}", string.Empty)
            .Replace("{{DETAILS_SECTION}}", BuildListSection(workItemsLabel, workItemTitles))
            .Replace("{{REASON_SECTION}}", BuildTextSection(reasonLabel, reason))
            .Replace("{{ACTION_URL}}", E(actionUrl))
            .Replace("{{ACTION_LABEL}}", E(actionLabel))
            .Replace("{{YEAR}}", DateTime.UtcNow.Year.ToString());

        return new RenderedDeliveryEmail(subject, html, BuildTextBody(
            headline, recipientName, introduction, workItemsLabel, workItemTitles,
            reasonLabel, reason, actionLabel, actionUrl));
    }

    private static string BuildListSection(string? label, IReadOnlyList<string> values)
    {
        if (label is null || values.Count == 0)
        {
            return string.Empty;
        }

        var items = new StringBuilder();
        foreach (var value in values)
        {
            items.Append("<li style=\"margin-bottom:6px\">").Append(E(value)).Append("</li>");
        }

        return Section(label, $"<ul style=\"margin:0;padding-left:18px\">{items}</ul>");
    }

    private static string BuildTextSection(string? label, string? value) =>
        label is null || string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Section(label, E(value).Replace("\n", "<br>"));

    private static string Section(string label, string innerHtml) =>
        "<tr><td style=\"padding:0 32px 20px\">" +
        "<div style=\"font-size:11px;font-weight:800;letter-spacing:.6px;text-transform:uppercase;color:#9ca3af;padding-bottom:7px\">" +
        E(label) +
        "</div>" +
        "<div style=\"padding:15px;border:1px solid #eef0f2;border-radius:9px;background:#fafafa;color:#4b5563;font-size:14px;line-height:1.6\">" +
        innerHtml +
        "</div></td></tr>";

    private static string BuildTextBody(
        string headline,
        string recipientName,
        string introduction,
        string? workItemsLabel,
        IReadOnlyList<string> workItemTitles,
        string? reasonLabel,
        string? reason,
        string actionLabel,
        string actionUrl)
    {
        var text = new StringBuilder()
            .AppendLine(headline).AppendLine()
            .AppendLine(string.IsNullOrWhiteSpace(recipientName) ? "Hello," : $"Hello {recipientName},")
            .AppendLine(introduction);

        if (workItemsLabel is not null && workItemTitles.Count > 0)
        {
            text.AppendLine().AppendLine($"{workItemsLabel}:");
            foreach (var title in workItemTitles)
            {
                text.AppendLine($"- {title}");
            }
        }

        if (reasonLabel is not null && !string.IsNullOrWhiteSpace(reason))
        {
            text.AppendLine().AppendLine($"{reasonLabel}:").AppendLine(reason);
        }

        return text.AppendLine().AppendLine($"{actionLabel}: {actionUrl}").AppendLine()
            .AppendLine("This is an automatic email from GigBridge. Please do not reply.")
            .ToString();
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
