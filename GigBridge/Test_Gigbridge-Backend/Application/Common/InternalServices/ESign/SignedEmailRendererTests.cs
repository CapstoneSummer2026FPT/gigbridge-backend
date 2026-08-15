using Application.Common.InternalServices.ESign.Models;
using Application.Features.Contracts.Signing.Common.Sign.DTOs;
using Application.Common.InternalServices.ESign.Email;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.ESign;

public sealed class SignedEmailRendererTests
{
    [Fact]
    public void Render_UsesSignedEmailTemplateAndEncodesDynamicValues()
    {
        var renderer = new SignedEmailRenderer(TestTemplateReader.FromProjectTemplates());

        var result = renderer.Render(new SignedEmailModel(
            "Client <Admin>",
            "Website & API",
            "GB-2026-001"));

        Assert.Equal("[GigBridge] Hợp đồng GB-2026-001 đã hoàn tất", result.Subject);
        Assert.Contains("Xin chào Client &lt;Admin&gt;", result.HtmlBody);
        Assert.Contains("Website &amp; API", result.HtmlBody);
        Assert.Contains("GB-2026-001", result.HtmlBody);
        Assert.Contains("PDF hoàn chỉnh đã được đính kèm", result.HtmlBody);
        Assert.Contains("Xem trước nội dung PDF", result.HtmlBody);
        Assert.Contains("CLIENT", result.HtmlBody);
        Assert.Contains("FREELANCER", result.HtmlBody);
        Assert.Contains("Tệp PDF hoàn chỉnh nằm trong phần đính kèm", result.HtmlBody);
        Assert.DoesNotContain("{{", result.HtmlBody);
        Assert.Contains("Bản PDF hoàn chỉnh được đính kèm", result.TextBody);
    }
}
