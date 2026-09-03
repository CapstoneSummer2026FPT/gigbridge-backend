using Application.Common.Interfaces.Email;
using Application.Common.Models.Email;
using Application.Features.Auth.Shared.DTOs;
using Microsoft.Extensions.Configuration;
using Resend;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.ExternalServices.Email.Resend;

public class EmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly IConfiguration _configuration;

    public EmailService(IResend resend, IConfiguration configuration)
    {
        _resend = resend;
        _configuration = configuration;
    }

    public async Task SendEmailAsync(EmailRequest emailRequestDTO, CancellationToken cancellationToken = default)
   {
        var fromEmail = _configuration["Resend:From"] ?? "onboarding@resend.dev";
        var fromName = _configuration["Resend:FromName"] ?? "GigBridge";

        var message = new EmailMessage();
        
        // Construct From: "FromName <fromEmail>" or just "fromEmail"
        message.From = string.IsNullOrWhiteSpace(fromName) ? fromEmail : $"{fromName} <{fromEmail}>";
        
        message.To.Add(emailRequestDTO.To);
        message.Subject = emailRequestDTO.Subject;

        if (emailRequestDTO.IsHtml)
        {
            message.HtmlBody = emailRequestDTO.Body;
            if (!string.IsNullOrWhiteSpace(emailRequestDTO.TextBody))
            {
                message.TextBody = emailRequestDTO.TextBody;
            }
        }
        else
        {
            message.TextBody = emailRequestDTO.Body;
        }

        if (!string.IsNullOrWhiteSpace(emailRequestDTO.MessageId))
        {
            message.Headers ??= new Dictionary<string, string>();
            message.Headers["Message-ID"] = emailRequestDTO.MessageId;
        }

        var attachments = await LoadAttachmentsAsync(emailRequestDTO.Attachments, cancellationToken);
        if (emailRequestDTO.ByteAttachments is { Count: > 0 })
        {
            attachments ??= new List<EmailAttachment>();
            attachments.AddRange(emailRequestDTO.ByteAttachments.Select(attachment => new EmailAttachment
            {
                Filename = attachment.FileName,
                Content = attachment.Content,
                ContentType = attachment.ContentType
            }));
        }
        if (attachments != null)
        {
            message.Attachments ??= new List<EmailAttachment>();
            message.Attachments.AddRange(attachments);
        }

        if (string.IsNullOrWhiteSpace(emailRequestDTO.IdempotencyKey))
        {
            await _resend.EmailSendAsync(message, cancellationToken);
        }
        else
        {
            await _resend.EmailSendAsync(emailRequestDTO.IdempotencyKey, message, cancellationToken);
        }
    }

    private static async Task<List<EmailAttachment>?> LoadAttachmentsAsync(IEnumerable<string>? attachments, CancellationToken cancellationToken)
    {
        if (attachments is null)
        {
            return null;
        }

        var list = new List<EmailAttachment>();
        foreach (var filePath in attachments.Where(File.Exists))
        {
            var attachment = await EmailAttachment.FromAsync(filePath, cancellationToken);
            list.Add(attachment);
        }

        return list.Count > 0 ? list : null;
    }
}
