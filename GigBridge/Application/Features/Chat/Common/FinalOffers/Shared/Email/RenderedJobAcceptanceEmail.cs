namespace Application.Features.Chat.Common.FinalOffers.Shared.Email;

public sealed record RenderedJobAcceptanceEmail(string Subject, string HtmlBody, string TextBody);
