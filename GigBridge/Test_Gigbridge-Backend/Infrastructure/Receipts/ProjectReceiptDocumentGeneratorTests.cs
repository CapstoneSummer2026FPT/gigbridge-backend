using Application.Common.InternalServices.Receipts.Models;
using Infrastructure.Adapters.Documents.Receipts;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Infrastructure.Receipts;

public sealed class ProjectReceiptDocumentGeneratorTests
{
    [Theory]
    [InlineData(ProjectReceiptType.Client)]
    [InlineData(ProjectReceiptType.Freelancer)]
    public void Generate_ProducesValidRoleTemplateWithAllPlaceholdersReplaced(ProjectReceiptType receiptType)
    {
        var generator = new ProjectReceiptDocumentGenerator(TestTemplateReader.FromProjectTemplates());
        var snapshot = CreateSnapshot(receiptType);
        var documentHash = new string('a', 64);

        var generated = generator.Generate(snapshot, documentHash);

        Assert.EndsWith(".docx", generated.FileName);
        Assert.NotEmpty(generated.Content);
        using var stream = new MemoryStream(generated.Content);
        using var document = WordprocessingDocument.Open(stream, false);
        var text = document.MainDocumentPart!.Document!.InnerText;
        Assert.DoesNotContain("{{", text);
        Assert.Contains(snapshot.ReceiptNumber, text);
        Assert.Contains("Thiết kế giao diện", text);
        Assert.Contains("Bàn giao hệ thống", text);
        Assert.Contains("Cá nhân", text);
        Assert.Contains("001234567890", text);
        Assert.Contains("123456789", text);
        Assert.Contains("BẮT ĐẦU THỰC TẾ", text);
        Assert.Contains("KẾT THÚC THỰC TẾ", text);
        Assert.Contains("TỔNG THỜI GIAN", text);
        Assert.Contains("HẠNG MỤC / TIẾN ĐỘ", text);
        Assert.Contains("22/07/2026 15:30:00", text);
        Assert.Contains("11/08/2026 14:30:00", text);
        Assert.Contains("19 ngày 23 giờ", text);
        Assert.Contains("Bắt đầu: ", text);
        Assert.Contains("Hạn dự kiến: ", text);
        Assert.Contains("Hoàn tất: ", text);
        Assert.Contains("Thời lượng: ", text);
        Assert.Contains("5 ngày", text);
        Assert.Equal(
            2,
            text.Split("MÃ ĐỊNH DANH / ID NUMBER", StringSplitOptions.None).Length - 1);
        Assert.Contains("THÔNG TIN QUY ĐỔI", text);
        Assert.DoesNotContain("USER ID", text);
        Assert.DoesNotContain(snapshot.Client.UserId.ToString("D"), text);
        Assert.DoesNotContain(snapshot.Freelancer.UserId.ToString("D"), text);
        Assert.DoesNotContain(snapshot.ContractCode, text);
        Assert.DoesNotContain(snapshot.ContractId.ToString("D"), text);
        Assert.DoesNotContain(snapshot.FinalTransactionReference, text);
        Assert.DoesNotContain("MÃ GIAO DỊCH CUỐI", text);
        Assert.DoesNotContain("SHA-256", text);
        Assert.DoesNotContain(documentHash, text);
        Assert.True(
            text.IndexOf("Thiết kế giao diện", StringComparison.Ordinal) <
            text.IndexOf("Bàn giao hệ thống", StringComparison.Ordinal));
        Assert.Empty(new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Microsoft365)
            .Validate(document));
    }

    private static ProjectReceiptSnapshot CreateSnapshot(ProjectReceiptType receiptType)
    {
        var now = new DateTime(2026, 8, 11, 8, 30, 0, DateTimeKind.Utc);
        return new ProjectReceiptSnapshot(
            Guid.NewGuid(),
            $"GB-RC-20260811-TEST-{receiptType}",
            (int)receiptType,
            now,
            "Hoàn tất / Completed",
            new ProjectReceiptPartySnapshot(
                Guid.NewGuid(), "Nguyễn Thị Khách Hàng", "Cá nhân", "client@example.com", "001234567890"),
            new ProjectReceiptPartySnapshot(
                Guid.NewGuid(), "Trần Văn Freelancer", "Cá nhân", "freelancer@example.com", "123456789"),
            "GB-CTR-2026-0001",
            Guid.NewGuid(),
            "Xây dựng nền tảng GigBridge",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 8, 10),
            now.AddHours(-1),
            1_000m,
            25m,
            1_025m,
            800m,
            200m,
            1_000m,
            0m,
            100m,
            1_000m,
            720m,
            180m,
            900m,
            "ESCROW-FINAL-TEST-001",
            1_000m,
            [
                new ProjectReceiptMilestoneSnapshot(
                    1, Guid.NewGuid(), "Thiết kế giao diện", now.AddDays(-10),
                    400m, 400m, 0m, 400m, 40m, 360m,
                    now.AddDays(-15), new DateOnly(2026, 8, 1)),
                new ProjectReceiptMilestoneSnapshot(
                    2, Guid.NewGuid(), "Bàn giao hệ thống", now.AddDays(-1),
                    600m, 400m, 200m, 600m, 60m, 540m,
                    now.AddDays(-9), new DateOnly(2026, 8, 10))
            ],
            now.AddDays(-20));
    }
}
