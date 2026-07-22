using System.Net;
using System.Reflection;
using System.Text;
using Application.Features.Chat.Common.FinalOffers.Shared.Email;

namespace Infrastructure.Services.Email;

public sealed class JobAcceptanceEmailRenderer : IJobAcceptanceEmailRenderer
{
    private const string LayoutResource = "ScheduleEmailTemplates/ScheduleEmail.html";
    private static readonly Assembly Assembly = typeof(ScheduleEmailRenderer).Assembly;

    public RenderedJobAcceptanceEmail Render(JobAcceptanceEmailModel model)
    {
        const string badge = "Application Accepted";
        const string headline = "Congratulations—you got the job!";
        var subject = $"You were accepted for {model.JobTitle} on GigBridge";
        var greeting = string.IsNullOrWhiteSpace(model.FreelancerName)
            ? "Hello,"
            : $"Hello {E(model.FreelancerName)},";
        var introduction = $"Great news! Your application for '{E(model.JobTitle)}' has been accepted and your contract is ready for the next steps.";

        var htmlBody = ReadResource(LayoutResource)
            .Replace("{{PREVIEW}}", E(headline))
            .Replace("{{BADGE_BACKGROUND}}", "#eef2ff")
            .Replace("{{ACCENT}}", "#494be7")
            .Replace("{{BADGE}}", badge)
            .Replace("{{HEADLINE}}", headline)
            .Replace("{{GREETING}}", greeting)
            .Replace("{{INTRODUCTION}}", introduction)
            .Replace("{{TITLE}}", E(model.JobTitle))
            .Replace("{{FORMATTED_TIME}}", E($"Final budget: {model.FinalBudget}"))
            .Replace("{{ACTOR_LABEL}}", "Status:")
            .Replace("{{ACTOR_NAME}}", "Accepted")
            .Replace("{{DETAILS_SECTION}}", "")
            .Replace("{{REASON_SECTION}}", "")
            .Replace("{{ACTION_URL}}", E(model.ActionUrl))
            .Replace("{{ACTION_LABEL}}", "View Contract")
            .Replace("{{YEAR}}", DateTime.UtcNow.Year.ToString());

        var textBody = new StringBuilder()
            .AppendLine(headline).AppendLine()
            .AppendLine(string.IsNullOrWhiteSpace(model.FreelancerName) ? "Hello," : $"Hello {model.FreelancerName},")
            .AppendLine($"Your application for '{model.JobTitle}' has been accepted.").AppendLine()
            .AppendLine($"Final budget: {model.FinalBudget}")
            .AppendLine($"View your contract: {model.ActionUrl}").AppendLine()
            .AppendLine("This is an automatic email from GigBridge. Please do not reply.")
            .ToString();

        return new RenderedJobAcceptanceEmail(subject, htmlBody, textBody);
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded email layout '{name}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
