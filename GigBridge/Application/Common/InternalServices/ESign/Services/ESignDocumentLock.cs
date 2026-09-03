namespace Application.Common.InternalServices.ESign.Services;

internal static class ESignDocumentLock
{
    private const long Namespace = 0x455349474E4C4F43;

    public static long ForDocument(Guid documentId) =>
        BitConverter.ToInt64(documentId.ToByteArray(), 0) ^ Namespace;
}
