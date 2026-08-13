using Application.Features.Receipts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Receipts;

public sealed class ProjectReceiptWorkflowTests
{
    [Fact]
    public async Task EnsurePair_QueuesVersionOneReceiptsForCorrectedTemplateRegeneration()
    {
        var contractId = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();
        var receipts = context.AddSet(
            CreateReadyReceipt(contractId, ProjectReceiptType.Client),
            CreateReadyReceipt(contractId, ProjectReceiptType.Freelancer));

        var result = await ProjectReceiptWorkflow.EnsurePairAsync(
            context,
            contractId,
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(receipts.Entities, receipt =>
        {
            Assert.Equal(2, receipt.TemplateVersion);
            Assert.Equal((int)ProjectReceiptGenerationStatus.Pending, receipt.GenerationStatus);
            Assert.Equal((int)ProjectReceiptEmailStatus.Pending, receipt.EmailStatus);
            Assert.Null(receipt.PdfContent);
            Assert.Null(receipt.GeneratedAt);
            Assert.Null(receipt.EmailedAt);
        });
    }

    private static ProjectReceipt CreateReadyReceipt(Guid contractId, ProjectReceiptType type) => new()
    {
        ProjectReceiptId = Guid.NewGuid(),
        ContractsId = contractId,
        OwnerUserId = Guid.NewGuid(),
        ReceiptType = (int)type,
        ReceiptNumber = $"TEST-{type}",
        TemplateVersion = 1,
        IssuedAt = DateTime.UtcNow,
        SnapshotJson = "{}",
        SnapshotHashSha256 = new string('a', 64),
        GenerationStatus = (int)ProjectReceiptGenerationStatus.Ready,
        NextGenerationAttemptAt = DateTime.UtcNow,
        PdfContent = [1, 2, 3],
        GeneratedAt = DateTime.UtcNow,
        EmailStatus = (int)ProjectReceiptEmailStatus.Delivered,
        NextEmailAttemptAt = DateTime.UtcNow,
        EmailedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };
}
