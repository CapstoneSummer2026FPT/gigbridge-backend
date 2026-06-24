namespace Application.Features.Proposals.Common.Email;

public sealed record RenderedProposalNegotiationEmail(string Subject, string HtmlBody, string TextBody);
