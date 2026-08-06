namespace Application.Common.Interfaces.IService;

public interface IWordToPdfConverter
{
    Task<byte[]> ConvertAsync(
        byte[] documentContent,
        string documentFileName,
        CancellationToken cancellationToken);
}
