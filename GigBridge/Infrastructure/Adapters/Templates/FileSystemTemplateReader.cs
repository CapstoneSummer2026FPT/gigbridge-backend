using System.Text;
using Application.Common.Interfaces.Templates;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Adapters.Templates;

public sealed class FileSystemTemplateReader : ITemplateReader
{
    private readonly string _templateRoot;
    private readonly string _templateRootWithSeparator;
    private readonly StringComparison _pathComparison;

    public FileSystemTemplateReader(IHostEnvironment environment)
    {
        _templateRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "Templates"));
        _templateRootWithSeparator = _templateRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    public string ReadText(string relativePath) =>
        File.ReadAllText(ResolveExistingPath(relativePath), Encoding.UTF8);

    public Task<string> ReadTextAsync(
        string relativePath,
        CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(ResolveExistingPath(relativePath), Encoding.UTF8, cancellationToken);

    public Stream OpenRead(string relativePath) =>
        new FileStream(
            ResolveExistingPath(relativePath),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

    private string ResolveExistingPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Template path is required.", nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Template path must be relative.", nameof(relativePath));
        }

        var normalized = relativePath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_templateRoot, normalized));

        if (!fullPath.StartsWith(_templateRootWithSeparator, _pathComparison))
        {
            throw new ArgumentException("Template path must stay within the template root.", nameof(relativePath));
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Template '{relativePath}' was not found.", fullPath);
        }

        return fullPath;
    }
}
