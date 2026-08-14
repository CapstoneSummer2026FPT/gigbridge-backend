using Application.Common.Interfaces.Documents;

namespace Test_Gigbridge_Backend.TestSupport;

internal sealed class FakeWordToPdfConverter : IWordToPdfConverter
{
    public List<ConvertCall> ConvertCalls { get; } = [];

    public Task<byte[]> ConvertAsync(
        byte[] documentContent,
        string documentFileName,
        CancellationToken cancellationToken)
    {
        ConvertCalls.Add(new ConvertCall(documentContent, documentFileName));
        return Task.FromResult<byte[]>([0x25, 0x50, 0x44, 0x46, 0x2d]);
    }

    internal sealed record ConvertCall(byte[] DocumentContent, string DocumentFileName);
}
