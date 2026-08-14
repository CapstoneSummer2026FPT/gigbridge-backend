namespace Application.Common.Interfaces.Documents;

public interface IWordToPdfConverter
{
    Task<byte[]> ConvertAsync(
        byte[] documentContent,
        string documentFileName,
        CancellationToken cancellationToken);
}
