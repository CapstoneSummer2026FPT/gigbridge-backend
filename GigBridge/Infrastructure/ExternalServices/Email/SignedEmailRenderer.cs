using System.Net;
using System.Text;
using Application.Common.Interfaces.Templates;
using Application.Features.ESign.Common.Interfaces;
using Application.Features.Contracts.Signing.Common.Sign.DTOs;

namespace Infrastructure.Services.Email;

public sealed class SignedEmailRenderer : ISignedEmailRenderer
{
    private const string TemplatePath = "Contracts/Signing/Email/SignedEmail.html";
    private readonly ITemplateReader _templateReader;

    public SignedEmailRenderer(ITemplateReader templateReader)
    {
        _templateReader = templateReader;
    }

    public RenderedSignedEmail Render(SignedEmailModel model)
    {
        var recipientName = E(model.RecipientName);
        var contractTitle = E(model.ContractTitle);
        var contractCode = E(model.ContractCode);
        var subject = $"[GigBridge] Hợp đồng {model.ContractCode} đã hoàn tất";
        var htmlBody = ReadTemplate()
            .Replace("{{PREVIEW}}", $"Hợp đồng {contractCode} đã được ký đầy đủ")
            .Replace("{{RECIPIENT_NAME}}", recipientName)
            .Replace("{{CONTRACT_TITLE}}", contractTitle)
            .Replace("{{CONTRACT_CODE}}", contractCode)
            .Replace("{{YEAR}}", DateTime.UtcNow.Year.ToString());
        var textBody = new StringBuilder()
            .AppendLine($"Xin chào {model.RecipientName},")
            .AppendLine()
            .AppendLine($"Hợp đồng {model.ContractTitle} ({model.ContractCode}) đã được Client và Freelancer ký đầy đủ.")
            .AppendLine("Bản PDF hoàn chỉnh được đính kèm email này. Hãy mở tệp đính kèm để xem toàn bộ nội dung và chữ ký.")
            .AppendLine()
            .AppendLine("Đây là email tự động từ GigBridge. Vui lòng không trả lời email này.")
            .ToString();

        return new RenderedSignedEmail(subject, htmlBody, textBody);
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private string ReadTemplate()
    {
        return _templateReader.ReadText(TemplatePath);
    }
}
