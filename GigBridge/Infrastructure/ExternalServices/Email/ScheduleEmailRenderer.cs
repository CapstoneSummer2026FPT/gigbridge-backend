using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Application.Features.Chat.Common.Schedules;

namespace Infrastructure.Services.Email;

public sealed class ScheduleEmailRenderer : IScheduleEmailRenderer
{
    private const string LayoutResource = "ScheduleEmailTemplates/ScheduleEmail.html";
    private static readonly Assembly Assembly = typeof(ScheduleEmailRenderer).Assembly;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RenderedScheduleEmail Render(ScheduleNotificationType type, ScheduleEmailModel model)
    {
        var copy = CopyFor(type, model);
        var actionUrl = type == ScheduleNotificationType.MeetingStarting && !string.IsNullOrWhiteSpace(model.MeetingUrl)
            ? model.MeetingUrl!
            : model.ScheduleUrl;
        var actionLabel = type == ScheduleNotificationType.MeetingStarting && !string.IsNullOrWhiteSpace(model.MeetingUrl)
            ? "Join meeting"
            : "View schedule";
        return new RenderedScheduleEmail(copy.Subject, RenderHtml(model, copy, actionLabel, actionUrl),
            RenderText(model, copy, actionLabel, actionUrl));
    }

    private static EmailCopy CopyFor(ScheduleNotificationType type, ScheduleEmailModel model)
    {
        var definition = JsonSerializer.Deserialize<EmailTemplateDefinition>(
            ReadResource($"ScheduleEmailTemplates/{type}.json"), JsonOptions)
            ?? throw new InvalidOperationException($"Schedule email template '{type}' is empty.");
        var copy = model.IsActor ? definition.Actor : definition.Recipient;
        return copy with
        {
            Subject = Expand(copy.Subject, model),
            Introduction = Expand(copy.Introduction, model)
        };
    }

    private static string RenderHtml(ScheduleEmailModel model, EmailCopy copy, string actionLabel, string actionUrl)
    {
        var greeting = string.IsNullOrWhiteSpace(model.RecipientName) ? "Hello," : $"Hello {E(model.RecipientName)},";
        var details = Section("Details", model.Details);
        var reason = Section("Reason", model.CancellationReason);
        return ReadResource(LayoutResource)
            .Replace("{{PREVIEW}}", E(copy.Preview))
            .Replace("{{BADGE_BACKGROUND}}", copy.BadgeBackground)
            .Replace("{{ACCENT}}", copy.Accent)
            .Replace("{{BADGE}}", E(copy.Badge))
            .Replace("{{HEADLINE}}", E(copy.Headline))
            .Replace("{{GREETING}}", greeting)
            .Replace("{{INTRODUCTION}}", E(copy.Introduction))
            .Replace("{{TITLE}}", E(model.Title))
            .Replace("{{FORMATTED_TIME}}", E(model.FormattedTime))
            .Replace("{{ACTOR_LABEL}}", E(copy.ActorLabel))
            .Replace("{{ACTOR_NAME}}", E(model.ActorName))
            .Replace("{{DETAILS_SECTION}}", details)
            .Replace("{{REASON_SECTION}}", reason)
            .Replace("{{ACTION_URL}}", E(actionUrl))
            .Replace("{{ACTION_LABEL}}", E(actionLabel))
            .Replace("{{YEAR}}", DateTime.UtcNow.Year.ToString());
    }

    private static string RenderText(ScheduleEmailModel model, EmailCopy copy, string actionLabel, string actionUrl)
    {
        var text = new StringBuilder()
            .AppendLine(copy.Headline).AppendLine()
            .AppendLine(string.IsNullOrWhiteSpace(model.RecipientName) ? "Hello," : $"Hello {model.RecipientName},")
            .AppendLine(copy.Introduction).AppendLine()
            .AppendLine(model.Title)
            .AppendLine(model.FormattedTime)
            .AppendLine($"{copy.ActorLabel} {model.ActorName}");
        if (!string.IsNullOrWhiteSpace(model.Details)) text.AppendLine().AppendLine("Details:").AppendLine(model.Details);
        if (!string.IsNullOrWhiteSpace(model.CancellationReason)) text.AppendLine().AppendLine("Reason:").AppendLine(model.CancellationReason);
        return text.AppendLine().AppendLine($"{actionLabel}: {actionUrl}").AppendLine()
            .AppendLine("This is an automatic scheduling email from GigBridge. Please do not reply.").ToString();
    }

    private static string Section(string label, string? value) => string.IsNullOrWhiteSpace(value)
        ? ""
        : $"<tr><td style=\"padding:0 32px 20px\"><div style=\"font-size:11px;font-weight:800;letter-spacing:.6px;text-transform:uppercase;color:#9ca3af;padding-bottom:7px\">{E(label)}</div><div style=\"padding:15px;border:1px solid #eef0f2;border-radius:9px;background:#fafafa;color:#4b5563;font-size:14px;line-height:1.6\">{E(value).Replace("\n", "<br>")}</div></td></tr>";

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Expand(string value, ScheduleEmailModel model) => value
        .Replace("{{TITLE}}", model.Title)
        .Replace("{{ACTOR_NAME}}", model.ActorName);

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded schedule email template '{name}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed record EmailTemplateDefinition(EmailCopy Actor, EmailCopy Recipient);

    private sealed record EmailCopy(string Subject, string Badge, string Headline, string Introduction,
        string Accent, string BadgeBackground, string ActorLabel)
    {
        public string Preview => $"{Headline}. {Introduction}";
    }
}
