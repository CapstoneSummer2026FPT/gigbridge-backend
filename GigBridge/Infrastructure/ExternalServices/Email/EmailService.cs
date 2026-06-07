using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace Infrastructure.Services.Email;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(EmailRequest emailRequestDTO, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
        var port = GetSmtpPort();
        var userName = GetRequiredSetting("Smtp:User", "SMTP_USER");
        var password = GetRequiredSetting("Smtp:Password", "SMTP_PASSWORD");
        var from = _configuration["Smtp:From"] ?? userName;

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(userName, password),
            EnableSsl = true
        };

        using var message = new MailMessage
        {
            From = new MailAddress(from),
            Subject = emailRequestDTO.Subject,
            Body = emailRequestDTO.Body,
            IsBodyHtml = emailRequestDTO.IsHtml
        };

        message.To.Add(emailRequestDTO.To);
        AddAttachments(message, emailRequestDTO.Attachments);

        await client.SendMailAsync(message, cancellationToken);
    }

    private int GetSmtpPort()
    {
        var configuredPort = _configuration["Smtp:Port"];
        return int.TryParse(configuredPort, out var port) ? port : 587;
    }

    private string GetRequiredSetting(string configurationKey, string environmentVariable)
    {
        var value = _configuration[configurationKey] ?? Environment.GetEnvironmentVariable(environmentVariable);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{configurationKey} is required to send email.");
        }

        return value;
    }

    private static void AddAttachments(MailMessage message, IEnumerable<string>? attachments)
    {
        if (attachments is null)
        {
            return;
        }

        foreach (var filePath in attachments.Where(File.Exists))
        {
            message.Attachments.Add(new Attachment(filePath));
        }
    }
}
