using System.Net;
using System.Net.Http.Headers;
using Application.Features.Contracts.Common.DTOs;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Infrastructure.Adapters.Documents.ESign;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Infrastructure.Adapters.Documents.ESign;

public sealed class ContractEsignDocumentGeneratorTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAF/gL+XxR8WQAAAABJRU5ErkJggg==");

    [Fact]
    public async Task GenerateAsync_ProducesValidDocxWithoutDeveloperPlaceholders()
    {
        var generator = new ContractEsignDocumentGenerator(
            new HttpClient(new SignatureHandler()),
            TestTemplateReader.FromProjectTemplates());
        var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
        var snapshot = CreateSnapshot(now);

        var preview = generator.RenderPreview(snapshot);
        Assert.Contains("Hợp đồng kiểm thử", preview);
        Assert.DoesNotContain("{{", preview);
        Assert.DoesNotContain("PHỤ LỤC D", preview);

        var clientSignature = CreateSignature(snapshot.Client.UserId, 0, now);
        var freelancerSignature = CreateSignature(snapshot.Freelancer.UserId, 1, now.AddMinutes(1));
        var generated = await generator.GenerateAsync(
            snapshot,
            clientSignature,
            freelancerSignature,
            new string('a', 64),
            CancellationToken.None);
        Assert.EndsWith(".docx", generated.FileName);
        using var stream = new MemoryStream(generated.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        var mainPart = document.MainDocumentPart!;
        var text = mainPart.Document!.InnerText;
        Assert.DoesNotContain("{{", text);
        Assert.DoesNotContain("PHỤ LỤC D", text);
        Assert.Contains("Không cung cấp", text);
        Assert.Contains("127.0.0.1", text);
        Assert.Equal(1, CountOccurrences(text, "Bên A và Bên B sau đây gọi riêng là “Bên”"));
        Assert.True(text.IndexOf("Milestone 1", StringComparison.Ordinal) <
                    text.IndexOf("Milestone 2", StringComparison.Ordinal));
        Assert.Equal(2, mainPart.ImageParts.Count());
        var finalBody = mainPart.Document.Body!;
        Assert.DoesNotContain(
            finalBody.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>(),
            item => item.Descendants<DocumentFormat.OpenXml.Wordprocessing.Break>()
                .Any(lineBreak => lineBreak.Type?.Value == DocumentFormat.OpenXml.Wordprocessing.BreakValues.Page));
        Assert.Contains(
            finalBody.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>(),
            item => item.InnerText.Contains("THÔNG TIN GIAO KẾT", StringComparison.Ordinal) &&
                    item.ParagraphProperties?.PageBreakBefore is not null);
        Assert.Empty(new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Microsoft365).Validate(document));
    }

    [Fact]
    public async Task GenerateAsync_EmbedsEachAvailableSignatureIndependently()
    {
        var generator = new ContractEsignDocumentGenerator(
            new HttpClient(new SignatureHandler()),
            TestTemplateReader.FromProjectTemplates());
        var now = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
        var snapshot = CreateSnapshot(now);

        var generated = await generator.GenerateAsync(
            snapshot,
            CreateSignature(snapshot.Client.UserId, 0, now) with { IsFinalized = false },
            null,
            new string('b', 64),
            CancellationToken.None);

        using var stream = new MemoryStream(generated.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        Assert.Single(document.MainDocumentPart!.ImageParts);
        var text = document.MainDocumentPart.Document!.InnerText;
        Assert.Contains("BẢN XEM TRƯỚC – CHƯA CÓ HIỆU LỰC", text);
        Assert.Contains("Bản tạm – chưa có hiệu lực", text);
        Assert.DoesNotContain("{{", text);
        var body = document.MainDocumentPart.Document.Body!;
        var previewNotice = body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
            .Single(item => item.InnerText.Contains("BẢN XEM TRƯỚC – CHƯA CÓ HIỆU LỰC", StringComparison.Ordinal));
        Assert.NotNull(previewNotice.ParagraphProperties?.PageBreakBefore);
        Assert.DoesNotContain(
            body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>(),
            item => item.Descendants<DocumentFormat.OpenXml.Wordprocessing.Break>()
                .Any(lineBreak => lineBreak.Type?.Value == DocumentFormat.OpenXml.Wordprocessing.BreakValues.Page));
        Assert.Empty(new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Microsoft365).Validate(document));
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static ContractDocumentSnapshot CreateSnapshot(DateTime now)
    {
        var client = new ContractPartySnapshot(
            Guid.NewGuid(), Guid.NewGuid(), "Nguyễn Văn Client", "client@example.com", "0900000001", "TP.HCM");
        var freelancer = new ContractPartySnapshot(
            Guid.NewGuid(), Guid.NewGuid(), "Trần Văn Freelancer", "freelancer@example.com", "0900000002", "Hà Nội");
        return new ContractDocumentSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "GB-TEST-0001", 1, 1, "Ver 1.0 Gigbridge", now,
            new DateOnly(2026, 7, 21), new DateOnly(2026, 8, 21),
            "Hợp đồng kiểm thử", "Mô tả dự án", "Phát triển và bàn giao", "Không bao gồm hosting", 1_000_000m,
            client, freelancer,
            [
                new ContractMilestoneSnapshot(1, "Milestone 1", "Xây dựng", "Mã nguồn", "Chạy đúng", new DateOnly(2026, 7, 21), new DateOnly(2026, 8, 1), "2 lần", 600_000m),
                new ContractMilestoneSnapshot(2, "Milestone 2", null, null, null, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 21), null, 400_000m)
            ]);
    }

    private static ContractSignatureSnapshot CreateSignature(Guid userId, int role, DateTime signedAt) =>
        new(userId, role, "https://res.cloudinary.com/gigbridge/signature.png", 300, 100, signedAt, "127.0.0.1", "xunit", "Ver 1.0 Gigbridge", signedAt);

    private sealed class SignatureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new ByteArrayContent(Png);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
