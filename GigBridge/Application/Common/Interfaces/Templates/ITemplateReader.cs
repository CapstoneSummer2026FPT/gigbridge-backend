namespace Application.Common.Interfaces.Templates;

public interface ITemplateReader
{
    string ReadText(string relativePath);

    Task<string> ReadTextAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Stream OpenRead(string relativePath);
}
