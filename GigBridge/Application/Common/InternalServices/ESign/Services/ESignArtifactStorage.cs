using System.Security.Cryptography;
using Application.Common.Interfaces;
using Application.Common.InternalServices.ESign.Models;
using Domain.Entities;
using Domain.Enums.ESign;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.InternalServices.ESign.Services;

internal static class ESignArtifactStorage
{
    public static async Task<ESignArtifactData?> GetAsync(
        IApplicationDbContext context,
        Guid documentId,
        ESignArtifactType artifactType,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var artifact = await context.Set<EsignDocumentArtifact>()
            .AsNoTracking()
            .TagWith($"ESign.Artifact.{artifactType}")
            .Where(item =>
                item.EsignDocumentsId == documentId &&
                item.ArtifactType == (int)artifactType)
            .Select(item => new ESignArtifactData(
                item.Content,
                item.FileName,
                item.MimeType,
                item.SizeBytes,
                item.ContentHashSha256,
                item.ArtifactRevision))
            .SingleOrDefaultAsync(cancellationToken);

        if (artifact is not null)
        {
            ESignTelemetry.RecordArtifactRead(artifactType, endpoint, artifact.SizeBytes);
            return artifact;
        }

        // Rolling-deploy fallback. Projection is intentionally artifact-specific so the
        // unrelated bytea column and text snapshot never leave PostgreSQL.
        var legacyArtifact = artifactType switch
        {
            ESignArtifactType.Pdf => await context.Set<EsignDocumentContent>()
                .AsNoTracking()
                .TagWith("ESign.Artifact.Pdf.LegacyFallback")
                .Where(item => item.EsignDocumentsId == documentId && item.PdfDocumentContent != null)
                .Select(item => new ESignArtifactData(
                    item.PdfDocumentContent!,
                    item.PdfDocumentFileName ?? string.Empty,
                    "application/pdf",
                    (long)item.PdfDocumentContent!.Length,
                    string.Empty,
                    0))
                .SingleOrDefaultAsync(cancellationToken),
            ESignArtifactType.FinalizedDocx => await context.Set<EsignDocumentContent>()
                .AsNoTracking()
                .TagWith("ESign.Artifact.Docx.LegacyFallback")
                .Where(item => item.EsignDocumentsId == documentId && item.FinalizedDocumentContent != null)
                .Select(item => new ESignArtifactData(
                    item.FinalizedDocumentContent!,
                    string.Empty,
                    item.FinalizedDocumentMimeType ?? "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    (long)item.FinalizedDocumentContent!.Length,
                    string.Empty,
                    0))
                .SingleOrDefaultAsync(cancellationToken),
            _ => null
        };
        if (legacyArtifact is not null)
        {
            ESignTelemetry.RecordArtifactRead(artifactType, endpoint, legacyArtifact.SizeBytes);
        }

        return legacyArtifact;
    }

    public static async Task UpsertAsync(
        IApplicationDbContext context,
        EsignDocument document,
        ESignArtifactType artifactType,
        byte[] content,
        string fileName,
        string mimeType,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (!context.SupportsRelationalBulkOperations)
        {
            var artifact = context.Set<EsignDocumentArtifact>()
                .SingleOrDefault(item =>
                    item.EsignDocumentsId == document.EsignDocumentsId &&
                    item.ArtifactType == (int)artifactType);
            if (artifact is null)
            {
                artifact = new EsignDocumentArtifact
                {
                    EsignDocumentArtifactId = Guid.NewGuid(),
                    EsignDocumentsId = document.EsignDocumentsId,
                    ArtifactType = (int)artifactType,
                    CreatedAt = now
                };
                context.Set<EsignDocumentArtifact>().Add(artifact);
            }

            artifact.Content = content;
            artifact.FileName = fileName;
            artifact.MimeType = mimeType;
            artifact.SizeBytes = content.LongLength;
            artifact.ContentHashSha256 = hash;
            artifact.ArtifactRevision = document.ContentRevision;
            artifact.UpdatedAt = now;
            UpdateLegacyInMemory(context, document.EsignDocumentsId, artifactType, content, fileName, mimeType);
            return;
        }

        var updated = await context.Set<EsignDocumentArtifact>()
            .Where(item =>
                item.EsignDocumentsId == document.EsignDocumentsId &&
                item.ArtifactType == (int)artifactType)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Content, content)
                .SetProperty(item => item.FileName, fileName)
                .SetProperty(item => item.MimeType, mimeType)
                .SetProperty(item => item.SizeBytes, content.LongLength)
                .SetProperty(item => item.ContentHashSha256, hash)
                .SetProperty(item => item.ArtifactRevision, document.ContentRevision)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);

        if (updated == 0)
        {
            context.Set<EsignDocumentArtifact>().Add(new EsignDocumentArtifact
            {
                EsignDocumentArtifactId = Guid.NewGuid(),
                EsignDocumentsId = document.EsignDocumentsId,
                ArtifactType = (int)artifactType,
                Content = content,
                FileName = fileName,
                MimeType = mimeType,
                SizeBytes = content.LongLength,
                ContentHashSha256 = hash,
                ArtifactRevision = document.ContentRevision,
                CreatedAt = now
            });
        }

        // Keep the legacy columns synchronized for one rolling-deploy window. ExecuteUpdate
        // changes only the selected bytea column and does not materialize the heavy row.
        var legacy = context.Set<EsignDocumentContent>()
            .Where(item => item.EsignDocumentsId == document.EsignDocumentsId);
        if (artifactType == ESignArtifactType.Pdf)
        {
            await legacy.ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.PdfDocumentContent, content)
                .SetProperty(item => item.PdfDocumentFileName, fileName), cancellationToken);
        }
        else
        {
            await legacy.ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.FinalizedDocumentContent, content)
                .SetProperty(item => item.FinalizedDocumentMimeType, mimeType), cancellationToken);
        }
    }

    public static async Task DeleteAsync(
        IApplicationDbContext context,
        Guid documentId,
        ESignArtifactType artifactType,
        CancellationToken cancellationToken)
    {
        if (!context.SupportsRelationalBulkOperations)
        {
            var artifacts = context.Set<EsignDocumentArtifact>()
                .Where(item =>
                    item.EsignDocumentsId == documentId &&
                    item.ArtifactType == (int)artifactType)
                .ToList();
            context.Set<EsignDocumentArtifact>().RemoveRange(artifacts);
            UpdateLegacyInMemory(context, documentId, artifactType, null, null, null);
            return;
        }

        await context.Set<EsignDocumentArtifact>()
            .Where(item =>
                item.EsignDocumentsId == documentId &&
                item.ArtifactType == (int)artifactType)
            .ExecuteDeleteAsync(cancellationToken);

        var legacy = context.Set<EsignDocumentContent>()
            .Where(item => item.EsignDocumentsId == documentId);
        if (artifactType == ESignArtifactType.Pdf)
        {
            await legacy.ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.PdfDocumentContent, (byte[]?)null)
                .SetProperty(item => item.PdfDocumentFileName, (string?)null), cancellationToken);
        }
        else
        {
            await legacy.ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.FinalizedDocumentContent, (byte[]?)null)
                .SetProperty(item => item.FinalizedDocumentMimeType, (string?)null), cancellationToken);
        }
    }

    private static void UpdateLegacyInMemory(
        IApplicationDbContext context,
        Guid documentId,
        ESignArtifactType artifactType,
        byte[]? content,
        string? fileName,
        string? mimeType)
    {
        var legacy = context.Set<EsignDocumentContent>()
            .SingleOrDefault(item => item.EsignDocumentsId == documentId);
        if (legacy is null)
        {
            return;
        }

        if (artifactType == ESignArtifactType.Pdf)
        {
            legacy.PdfDocumentContent = content;
            legacy.PdfDocumentFileName = fileName;
        }
        else
        {
            legacy.FinalizedDocumentContent = content;
            legacy.FinalizedDocumentMimeType = mimeType;
        }
    }
}
