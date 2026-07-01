using System.Net;
using System.Text;
using Application.Features.JobInvitations.Common.Email;
using Microsoft.AspNetCore.Hosting;

namespace Infrastructure.Services.Email;

public sealed class JobInvitationEmailRenderer : IJobInvitationEmailRenderer
{
    private const string TemplateName = "NewJobInvitationTemplate.html";
    private const string Subject = "You have received a new job invitation";
    private readonly IWebHostEnvironment _webHostEnvironment;

    public JobInvitationEmailRenderer(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
    }

    public RenderedJobInvitationEmail Render(NewJobInvitationTemplate model)
    {
        var htmlBody = ReadTemplate()
            .Replace("{{FREELANCER_NAME}}", E(model.FreelancerName))
            .Replace("{{JOB_TITLE}}", E(model.JobTitle))
            .Replace("{{CLIENT_NAME}}", E(model.ClientName))
            .Replace("{{BUDGET}}", E(model.Budget))
            .Replace("{{DEADLINE}}", E(model.Deadline))
            .Replace("{{SHORT_DESCRIPTION}}", E(model.ShortDescription).Replace("\n", "<br>"))
            .Replace("{{JOB_DETAIL_URL}}", E(model.ActionUrl))
            .Replace("{{YEAR}}", DateTime.UtcNow.Year.ToString());

        var textBody = new StringBuilder()
            .AppendLine(Subject).AppendLine()
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

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private string ReadTemplate()
    {
        var path = Path.Combine(_webHostEnvironment.ContentRootPath, "Templates", TemplateName);
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
