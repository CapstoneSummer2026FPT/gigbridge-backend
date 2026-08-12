namespace Application.Features.ESign.Common.Interfaces;

public interface IWordToPdfConverter
{
    Task<byte[]> ConvertAsync(
        byte[] documentContent,
        string documentFileName,
        CancellationToken cancellationToken);
}
