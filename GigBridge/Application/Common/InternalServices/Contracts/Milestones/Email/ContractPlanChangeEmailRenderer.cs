using System.Net;
using System.Text;
using Application.Common.Interfaces.Templates;
using Application.Common.InternalServices.Contracts.Interfaces;
using Application.Common.InternalServices.Contracts.Models;

namespace Application.Common.InternalServices.Contracts.Milestones.Email;

public sealed class ContractPlanChangeEmailRenderer : IContractPlanChangeEmailRenderer
{
    private const string LayoutTemplate = "Common/Email/NotificationLayout.html";
    private readonly ITemplateReader _templateReader;

    public ContractPlanChangeEmailRenderer(ITemplateReader templateReader)
    {
        _templateReader = templateReader;
    }

    public RenderedContractPlanChangeEmail Render(ContractPlanChangeEmailModel model)
    {
        var subject = $"Project plan changes requested - {model.ContractTitle}";
        var headline = "Your project plan needs changes";
        var introduction = $"{model.FreelancerName} requested changes to the milestones and work breakdown structure.";
        var greeting = string.IsNullOrWhiteSpace(model.ClientName)
            ? "Hello,"
            : $"Hello {E(model.ClientName)},";

        var htmlBody = _templateReader.ReadText(LayoutTemplate)
            .Replace("{{PREVIEW}}", E($"{headline}. {introduction}"))
            .Replace("{{BADGE_BACKGROUND}}", "#fffbeb")
            .Replace("{{ACCENT}}", "#d97706")
            .Replace("{{BADGE}}", "Changes requested")
            .Replace("{{HEADLINE}}", headline)
            .Replace("{{GREETING}}", greeting)
            .Replace("{{INTRODUCTION}}", E(introduction))
            .Replace("{{TITLE}}", E(model.ContractTitle))
            .Replace("{{FORMATTED_TIME}}", "Please review and update the project plan")
            .Replace("{{ACTOR_LABEL}}", "Requested by:")
            .Replace("{{ACTOR_NAME}}", E(model.FreelancerName))
            .Replace("{{DETAILS_SECTION}}", string.Empty)
            .Replace("{{REASON_SECTION}}", BuildReasonSection(model.Reason))
            .Replace("{{ACTION_URL}}", E(model.ActionUrl))
            .Replace("{{ACTION_LABEL}}", "Review project plan")
            .Replace("{{YEAR}}", DateTime.UtcNow.Year.ToString());

        var textBody = new StringBuilder()
            .AppendLine(headline).AppendLine()
            .AppendLine(string.IsNullOrWhiteSpace(model.ClientName) ? "Hello," : $"Hello {model.ClientName},")
            .AppendLine(introduction).AppendLine()
            .AppendLine($"Contract: {model.ContractTitle}")
            .AppendLine($"Requested by: {model.FreelancerName}").AppendLine()
            .AppendLine("Reason:")
            .AppendLine(model.Reason).AppendLine()
            .AppendLine($"Review project plan: {model.ActionUrl}").AppendLine()
            .AppendLine("This is an automatic email from GigBridge. Please do not reply.")
            .ToString();

        return new RenderedContractPlanChangeEmail(subject, htmlBody, textBody);
    }

    private static string BuildReasonSection(string reason) =>
        "<tr><td style=\"padding:0 32px 20px\">" +
        "<div style=\"font-size:11px;font-weight:800;letter-spacing:.6px;text-transform:uppercase;color:#9ca3af;padding-bottom:7px\">Reason</div>" +
        "<div style=\"padding:15px;border:1px solid #eef0f2;border-radius:9px;background:#fafafa;color:#4b5563;font-size:14px;line-height:1.6\">" +
        E(reason).Replace("\n", "<br>") +
        "</div></td></tr>";

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
