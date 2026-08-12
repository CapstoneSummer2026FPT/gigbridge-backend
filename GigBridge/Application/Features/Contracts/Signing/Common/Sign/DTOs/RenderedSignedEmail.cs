namespace Application.Features.Contracts.Signing.Common.Sign.DTOs;

public sealed record RenderedSignedEmail(
    string Subject,
    string HtmlBody,
    string TextBody);
