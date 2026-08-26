using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.InternalServices.ESign.Models;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.InternalServices.ESign.Services;

internal static class ESignDocumentContentStorage
{
    public static async Task<ESignDocumentContentData> GetAsync(
        IApplicationDbContext context,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var content = await FindAsync(context, documentId, cancellationToken);

        return content ?? throw new NotFoundException("E-sign document content does not exist.");
    }

    public static Task<ESignDocumentContentData?> FindAsync(
        IApplicationDbContext context,
        Guid documentId,
        CancellationToken cancellationToken) =>
        context.Set<EsignDocumentContent>()
            .AsNoTracking()
            .TagWith("ESign.Content.Text")
            .Where(item => item.EsignDocumentsId == documentId)
            .Select(item => new ESignDocumentContentData(
                item.EsignDocumentsId,
                item.RenderedHtmlContent,
                item.ContractSnapshotJson))
            .SingleOrDefaultAsync(cancellationToken);

    public static async Task<int> UpdateTextAsync(
        IApplicationDbContext context,
        Guid documentId,
        string renderedHtmlContent,
        string? contractSnapshotJson,
        CancellationToken cancellationToken)
    {
        if (!context.SupportsRelationalBulkOperations)
        {
            var content = context.Set<EsignDocumentContent>()
                .SingleOrDefault(item => item.EsignDocumentsId == documentId);
            if (content is null)
            {
                return 0;
            }

            content.RenderedHtmlContent = renderedHtmlContent;
            content.ContractSnapshotJson = contractSnapshotJson;
            return 1;
        }

        return await context.Set<EsignDocumentContent>()
            .Where(item => item.EsignDocumentsId == documentId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.RenderedHtmlContent, renderedHtmlContent)
                .SetProperty(item => item.ContractSnapshotJson, contractSnapshotJson), cancellationToken);
    }

    public static async Task<int> UpdateSnapshotAsync(
        IApplicationDbContext context,
        Guid documentId,
        string contractSnapshotJson,
        CancellationToken cancellationToken)
    {
        if (!context.SupportsRelationalBulkOperations)
        {
            var content = context.Set<EsignDocumentContent>()
                .SingleOrDefault(item => item.EsignDocumentsId == documentId);
            if (content is null)
            {
                return 0;
            }

            content.ContractSnapshotJson = contractSnapshotJson;
            return 1;
        }

        return await context.Set<EsignDocumentContent>()
            .Where(item => item.EsignDocumentsId == documentId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ContractSnapshotJson, contractSnapshotJson), cancellationToken);
    }
}
