using System;
using System.IO;
using System.Net;
using System.Text;
using Application.Common.Interfaces.Templates;
using Application.Common.InternalServices.Proposals.Email;
using Application.Common.InternalServices.Proposals.Interfaces;
using Application.Common.InternalServices.Proposals.Models;

namespace Application.Common.InternalServices.Proposals.Email;
public sealed class ProposalNegotiationEmailRenderer : IProposalNegotiationEmailRenderer
{
    private const string LayoutTemplate = "Common/Email/NotificationLayout.html";
    private readonly ITemplateReader _templateReader;

    public ProposalNegotiationEmailRenderer(ITemplateReader templateReader)
    {
        _templateReader = templateReader;
    }

    public RenderedProposalNegotiationEmail Render(ProposalNegotiationEmailModel model)
    {
        var subject = "Your proposal was accepted for negotiation on GigBridge";
        var badge = "Proposal Accepted";
        var headline = "Your proposal was accepted for negotiation";
        var greeting = string.IsNullOrWhiteSpace(model.FreelancerName) ? "Hello," : $"Hello {E(model.FreelancerName)},";
        var introduction = $"Great news! {E(model.ClientName)} has accepted your proposal on the job '{E(model.JobTitle)}' to start negotiation.";

        var formattedTime = $"Budget: {model.ProposedBudget} | Duration: {model.ProposedDuration}";
        var actorLabel = "Accepted by:";
        var actorName = model.ClientName;

        var htmlBody = _templateReader.ReadText(LayoutTemplate)
            .Replace("{{PREVIEW}}", E(headline))
            .Replace("{{BADGE_BACKGROUND}}", "#ecfeff")
            .Replace("{{ACCENT}}", "#0891b2")
            .Replace("{{BADGE}}", E(badge))
            .Replace("{{HEADLINE}}", E(headline))
            .Replace("{{GREETING}}", greeting)
            .Replace("{{INTRODUCTION}}", introduction)
            .Replace("{{TITLE}}", E(model.JobTitle))
            .Replace("{{FORMATTED_TIME}}", E(formattedTime))
            .Replace("{{ACTOR_LABEL}}", E(actorLabel))
            .Replace("{{ACTOR_NAME}}", E(actorName))
            .Replace("{{DETAILS_SECTION}}", "")
            .Replace("{{REASON_SECTION}}", "")
            .Replace("{{ACTION_URL}}", E(model.ActionUrl))
            .Replace("{{ACTION_LABEL}}", "Open Negotiation Chat")
            .Replace("{{YEAR}}", DateTime.UtcNow.Year.ToString());

        var textBody = new StringBuilder()
            .AppendLine(headline).AppendLine()
            .AppendLine(string.IsNullOrWhiteSpace(model.FreelancerName) ? "Hello," : $"Hello {model.FreelancerName},")
            .AppendLine(introduction).AppendLine()
            .AppendLine($"Job: {model.JobTitle}")
            .AppendLine($"Proposed Budget: {model.ProposedBudget}")
            .AppendLine($"Proposed Duration: {model.ProposedDuration}").AppendLine()
            .AppendLine($"Open Negotiation Chat: {model.ActionUrl}").AppendLine()
            .AppendLine("This is an automatic email from GigBridge. Please do not reply.").ToString();

        return new RenderedProposalNegotiationEmail(subject, htmlBody, textBody);
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

}
