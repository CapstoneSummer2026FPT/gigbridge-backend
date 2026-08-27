using System.Security.Cryptography;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Receipts.Models;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.InternalServices.Receipts.Services;

internal static class ProjectReceiptArtifactStorage
{
    public static async Task<ProjectReceiptArtifactData?> GetPdfAsync(
        IApplicationDbContext context,
        Guid receiptId,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var artifact = await context.Set<ProjectReceiptArtifact>()
            .AsNoTracking()
            .TagWith($"Receipt.Artifact.Pdf.{endpoint}")
            .Where(item => item.ProjectReceiptId == receiptId &&
                item.ArtifactType == (int)ProjectReceiptArtifactType.Pdf)
            .Select(item => new ProjectReceiptArtifactData(
                item.Content, item.FileName, item.MimeType, item.SizeBytes,
                item.ContentHashSha256, item.ArtifactRevision))
            .SingleOrDefaultAsync(cancellationToken);
        if (artifact is not null) return artifact;

        return await context.Set<ProjectReceiptContent>()
            .AsNoTracking()
            .TagWith($"Receipt.Artifact.Pdf.LegacyFallback.{endpoint}")
            .Where(item => item.ProjectReceiptId == receiptId && item.PdfContent != null)
            .Select(item => new ProjectReceiptArtifactData(
                item.PdfContent!, item.PdfFileName ?? string.Empty,
                item.PdfContentType ?? "application/pdf", (long)item.PdfContent!.Length,
                item.PdfHashSha256 ?? string.Empty, 0))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public static Task<ProjectReceiptSnapshotData?> GetSnapshotAsync(
        IApplicationDbContext context,
        Guid receiptId,
        CancellationToken cancellationToken) =>
        context.Set<ProjectReceiptContent>()
            .AsNoTracking()
            .TagWith("Receipt.Content.Snapshot")
            .Where(item => item.ProjectReceiptId == receiptId)
            .Select(item => new ProjectReceiptSnapshotData(item.SnapshotJson, item.SnapshotHashSha256))
            .SingleOrDefaultAsync(cancellationToken);

    public static async Task UpsertPdfAsync(
        IApplicationDbContext context,
        ProjectReceipt receipt,
        byte[] content,
        string fileName,
        string mimeType,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (!context.SupportsRelationalBulkOperations)
        {
            var inMemory = context.Set<ProjectReceiptArtifact>().SingleOrDefault(item =>
                item.ProjectReceiptId == receipt.ProjectReceiptId &&
                item.ArtifactType == (int)ProjectReceiptArtifactType.Pdf);
            if (inMemory is null)
            {
                inMemory = new ProjectReceiptArtifact
                {
                    ProjectReceiptArtifactId = Guid.NewGuid(),
                    ProjectReceiptId = receipt.ProjectReceiptId,
                    ArtifactType = (int)ProjectReceiptArtifactType.Pdf,
                    CreatedAt = now
                };
                context.Set<ProjectReceiptArtifact>().Add(inMemory);
            }
            SetArtifact(inMemory, receipt, content, fileName, mimeType, hash, now);
            var inMemoryLegacy = context.Set<ProjectReceiptContent>()
                .Single(item => item.ProjectReceiptId == receipt.ProjectReceiptId);
            SetLegacy(inMemoryLegacy, content, fileName, mimeType, hash);
            return;
        }

        var updated = await context.Set<ProjectReceiptArtifact>()
            .Where(item => item.ProjectReceiptId == receipt.ProjectReceiptId &&
                item.ArtifactType == (int)ProjectReceiptArtifactType.Pdf)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Content, content)
                .SetProperty(item => item.FileName, fileName)
                .SetProperty(item => item.MimeType, mimeType)
                .SetProperty(item => item.SizeBytes, content.LongLength)
                .SetProperty(item => item.ContentHashSha256, hash)
                .SetProperty(item => item.ArtifactRevision, receipt.ContentRevision + 1)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (updated == 0)
        {
            var artifact = new ProjectReceiptArtifact
            {
                ProjectReceiptArtifactId = Guid.NewGuid(),
                ProjectReceiptId = receipt.ProjectReceiptId,
                ArtifactType = (int)ProjectReceiptArtifactType.Pdf,
                CreatedAt = now
            };
            SetArtifact(artifact, receipt, content, fileName, mimeType, hash, now);
            context.Set<ProjectReceiptArtifact>().Add(artifact);
        }

        // Rolling-deploy compatibility. Remove after the old backend has drained.
        await context.Set<ProjectReceiptContent>()
            .Where(item => item.ProjectReceiptId == receipt.ProjectReceiptId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.PdfContent, content)
                .SetProperty(item => item.PdfFileName, fileName)
                .SetProperty(item => item.PdfContentType, mimeType)
                .SetProperty(item => item.PdfHashSha256, hash), cancellationToken);
    }

    private static void SetArtifact(
        ProjectReceiptArtifact artifact, ProjectReceipt receipt, byte[] content,
        string fileName, string mimeType, string hash, DateTime now)
    {
        artifact.Content = content;
        artifact.FileName = fileName;
        artifact.MimeType = mimeType;
        artifact.SizeBytes = content.LongLength;
        artifact.ContentHashSha256 = hash;
        artifact.ArtifactRevision = receipt.ContentRevision + 1;
        artifact.UpdatedAt = now;
    }

    private static void SetLegacy(
        ProjectReceiptContent legacy, byte[] content, string fileName, string mimeType, string hash)
    {
        legacy.PdfContent = content;
        legacy.PdfFileName = fileName;
        legacy.PdfContentType = mimeType;
        legacy.PdfHashSha256 = hash;
    }
}
