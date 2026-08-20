namespace Application.Common.Models.Email;

public class EmailRequest
{
    public string To { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string? TextBody { get; set; }
    public bool IsHtml { get; set; } = true;
    public string? MessageId { get; set; }
    public string? IdempotencyKey { get; set; }
    public List<string>? Attachments { get; set; }
    public List<EmailByteAttachment>? ByteAttachments { get; set; }
}

public sealed record EmailByteAttachment(string FileName, byte[] Content, string ContentType);
