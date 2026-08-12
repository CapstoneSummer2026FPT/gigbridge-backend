namespace Application.Features.Contracts.Common.Email;

public sealed record RenderedSignedEmail(
    string Subject,
    string HtmlBody,
    string TextBody);
