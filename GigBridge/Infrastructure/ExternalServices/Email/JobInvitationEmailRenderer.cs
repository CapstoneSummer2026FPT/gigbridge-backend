using System.Net;
using System.Reflection;
using System.Text;
using Application.Features.JobInvitations.Common.Email;

namespace Infrastructure.Services.Email;

public sealed class JobInvitationEmailRenderer : IJobInvitationEmailRenderer
{
    private const string LayoutResource = "ScheduleEmailTemplates/ScheduleEmail.html";
    private const string Subject = "You have received a new job invitation";
    private static readonly Assembly Assembly = typeof(ScheduleEmailRenderer).Assembly;

    public RenderedJobInvitationEmail Render(NewJobInvitationTemplate model)
    {
        var headline = "You have received a new job invitation";
        var greeting = string.IsNullOrWhiteSpace(model.FreelancerName) ? "Hello," : $"Hello {E(model.FreelancerName)},";
        var introduction = $"{E(model.ClientName)} invited you to view and apply for a new job on GigBridge.";
        var formattedTime = $"Budget: {model.Budget} | Deadline: {model.Deadline}";
        var details = Section("Short description", model.ShortDescription);

        var htmlBody = ReadResource(LayoutResource)
            .Replace("{{PREVIEW}}", E(headline))
            .Replace("{{BADGE_BACKGROUND}}", "#ecfeff")
            .Replace("{{ACCENT}}", "#0891b2")
            .Replace("{{BADGE}}", "New Invitation")
            .Replace("{{HEADLINE}}", E(headline))
            .Replace("{{GREETING}}", greeting)
            .Replace("{{INTRODUCTION}}", introduction)
            .Replace("{{TITLE}}", E(model.JobTitle))
            .Replace("{{FORMATTED_TIME}}", E(formattedTime))
            .Replace("{{ACTOR_LABEL}}", "Client:")
            .Replace("{{ACTOR_NAME}}", E(model.ClientName))
            .Replace("{{DETAILS_SECTION}}", details)
            .Replace("{{REASON_SECTION}}", "")
            .Replace("{{ACTION_URL}}", E(model.ActionUrl))
            .Replace("{{ACTION_LABEL}}", "View job details")
            .Replace("{{YEAR}}", DateTime.UtcNow.Year.ToString());

        var textBody = new StringBuilder()
            .AppendLine(headline).AppendLine()
            .AppendLine(string.IsNullOrWhiteSpace(model.FreelancerName) ? "Hello," : $"Hello {model.FreelancerName},")
            .AppendLine($"{model.ClientName} invited you to view and apply for a new job on GigBridge.").AppendLine()
            .AppendLine($"Job: {model.JobTitle}")
            .AppendLine($"Client: {model.ClientName}")
            .AppendLine($"Budget: {model.Budget}")
            .AppendLine($"Deadline: {model.Deadline}")
            .AppendLine("Short description:")
            .AppendLine(model.ShortDescription).AppendLine()
            .AppendLine($"View job details: {model.ActionUrl}").AppendLine()
            .AppendLine("This is an automatic email from GigBridge. Please do not reply.")
            .ToString();

        return new RenderedJobInvitationEmail(Subject, htmlBody, textBody);
    }

    private static string Section(string label, string? value) => string.IsNullOrWhiteSpace(value)
        ? ""
        : $"<tr><td style=\"padding:0 32px 20px\"><div style=\"font-size:11px;font-weight:800;letter-spacing:.6px;text-transform:uppercase;color:#9ca3af;padding-bottom:7px\">{E(label)}</div><div style=\"padding:15px;border:1px solid #eef0f2;border-radius:9px;background:#fafafa;color:#4b5563;font-size:14px;line-height:1.6\">{E(value).Replace("\n", "<br>")}</div></td></tr>";

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded email layout '{name}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
