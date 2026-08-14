using Application.Common.InternalServices.Receipts.Models;

namespace Application.Common.Interfaces.Documents;

public interface IProjectReceiptDocumentGenerator
{
    GeneratedProjectReceiptDocument Generate(ProjectReceiptSnapshot snapshot, string documentHashSha256);
}
