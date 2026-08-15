namespace Application.Common.InternalServices.ESign.Models;
public sealed record RenderedSignedEmail(
    string Subject,
    string HtmlBody,
    string TextBody);
