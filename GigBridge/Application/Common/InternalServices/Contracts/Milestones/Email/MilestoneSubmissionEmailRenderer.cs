using System.Net;
using System.Text;
using Application.Common.Interfaces.Templates;
using Application.Common.InternalServices.Contracts.Interfaces;
using Application.Common.InternalServices.Contracts.Models;

namespace Application.Common.InternalServices.Contracts.Milestones.Email;
public sealed class MilestoneSubmissionEmailRenderer : IMilestoneSubmissionEmailRenderer
{
    private const string TemplatePath = "Contracts/Milestones/Email/MilestoneSubmittedEmail.html";
    private readonly ITemplateReader _templateReader;

    public MilestoneSubmissionEmailRenderer(ITemplateReader templateReader)
    {
        _templateReader = templateReader;
    }

    public RenderedMilestoneSubmissionEmail Render(MilestoneSubmissionEmailModel model)
    {
        var jobTitle = E(model.JobTitle);
        var milestoneTitle = E(model.MilestoneTitle);
        var subject = $"New Milestone Submission – {model.JobTitle} – {model.MilestoneTitle}";

        var htmlBody = ReadTemplate()
            .Replace("{{PREVIEW}}", $"{E(model.FreelancerName)} submitted a new deliverable for {jobTitle}")
            .Replace("{{CLIENT_NAME}}", E(model.ClientName))
            .Replace("{{FREELANCER_NAME}}", E(model.FreelancerName))
            .Replace("{{JOB_TITLE}}", jobTitle)
            .Replace("{{MILESTONE_TITLE}}", milestoneTitle)
            .Replace("{{MILESTONE_NUMBER}}", model.MilestoneNumber.ToString())
            .Replace("{{MILESTONE_COUNT}}", model.MilestoneCount.ToString())
            .Replace("{{START_DATE}}", E(FormatDate(model.StartDate)))
            .Replace("{{DEADLINE}}", E(FormatDate(model.Deadline)))
            .Replace("{{SUBMITTED_AT}}", E(FormatDateTime(model.SubmittedAt)))
            .Replace("{{STATUS_LABEL}}", E(model.StatusLabel))
            .Replace("{{FILES_ROWS}}", BuildFilesRowsHtml(model.Files))
            .Replace("{{ACTION_URL}}", E(model.ActionUrl))
            .Replace("{{YEAR}}", DateTime.UtcNow.Year.ToString());

        var textBody = BuildTextBody(model, subject);

        return new RenderedMilestoneSubmissionEmail(subject, htmlBody, textBody);
    }

    private static string BuildFilesRowsHtml(IReadOnlyList<MilestoneSubmissionFileModel> files)
    {
        if (files.Count == 0)
        {
            return "<tr><td style=\"padding:16px;font-size:13px;color:#6b7280;text-align:center\">No files attached</td></tr>";
        }

        var builder = new StringBuilder();
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var borderTop = i == 0 ? string.Empty : "border-top:1px solid #e5e7eb;";
            var sizeText = string.IsNullOrWhiteSpace(file.SizeLabel)
                ? E(file.TypeLabel)
                : $"{E(file.TypeLabel)} &middot; {E(file.SizeLabel)}";
            builder.Append("<tr><td style=\"padding:14px 16px;").Append(borderTop).Append("\">")
                .Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\"><tr>")
                .Append("<td style=\"width:36px;font-size:20px;vertical-align:middle\">").Append(file.IconGlyph).Append("</td>")
                .Append("<td style=\"vertical-align:middle;padding-left:6px\">")
                .Append("<div style=\"font-size:13px;font-weight:700;color:#111827;word-break:break-all\">").Append(E(file.FileName)).Append("</div>")
                .Append("<div style=\"padding-top:2px;font-size:12px;color:#6b7280\">").Append(sizeText).Append("</div>")
                .Append("</td></tr></table></td></tr>");
        }

        return builder.ToString();
    }

    private static string BuildTextBody(MilestoneSubmissionEmailModel model, string subject)
    {
        var builder = new StringBuilder()
            .AppendLine(subject).AppendLine()
            .AppendLine($"Hi {model.ClientName},")
            .AppendLine($"Freelancer {model.FreelancerName} has submitted a new deliverable for your project.").AppendLine()
            .AppendLine($"Job: {model.JobTitle}")
            .AppendLine($"Milestone: Milestone {model.MilestoneNumber} of {model.MilestoneCount} - {model.MilestoneTitle}")
            .AppendLine($"Start date: {FormatDate(model.StartDate)}")
            .AppendLine($"Deadline: {FormatDate(model.Deadline)}")
            .AppendLine($"Submitted: {FormatDateTime(model.SubmittedAt)}")
            .AppendLine($"Status: {model.StatusLabel}").AppendLine()
            .AppendLine("Submitted files:");

        if (model.Files.Count == 0)
        {
            builder.AppendLine("  (no files attached)");
        }
        else
        {
            foreach (var file in model.Files)
            {
                var sizeText = string.IsNullOrWhiteSpace(file.SizeLabel) ? file.TypeLabel : $"{file.TypeLabel} - {file.SizeLabel}";
                builder.AppendLine($"  - {file.FileName} ({sizeText})");
            }
        }

        builder.AppendLine()
            .AppendLine($"View submission: {model.ActionUrl}").AppendLine()
            .AppendLine("This is an automatic email from GigBridge. Please do not reply.");

        return builder.ToString();
    }

    private static string FormatDate(DateTime? value) =>
        value.HasValue ? value.Value.ToString("MMMM d, yyyy") : "Not started";

    private static string FormatDate(DateOnly? value) =>
        value.HasValue ? value.Value.ToString("MMMM d, yyyy") : "No deadline set";

    private static string FormatDateTime(DateTime value) =>
        value.ToString("MMMM d, yyyy 'at' HH:mm 'UTC'");

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private string ReadTemplate()
    {
        return _templateReader.ReadText(TemplatePath);
    }
}
