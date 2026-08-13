using Application.Common.Interfaces.Templates;

namespace Test_Gigbridge_Backend.TestSupport;

internal sealed class TestTemplateReader : ITemplateReader
{
    private readonly IReadOnlyDictionary<string, byte[]> _templates;

    private TestTemplateReader(IReadOnlyDictionary<string, byte[]> templates)
    {
        _templates = templates;
    }

    internal static TestTemplateReader FromProjectTemplates()
    {
        var root = FindProjectTemplateRoot();
        var templates = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.OrdinalIgnoreCase);
        return new TestTemplateReader(templates);
    }

    public string ReadText(string relativePath) =>
        System.Text.Encoding.UTF8.GetString(Get(relativePath));

    public Task<string> ReadTextAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReadText(relativePath));
    }

    public Stream OpenRead(string relativePath) =>
        new MemoryStream(Get(relativePath), writable: false);

    private byte[] Get(string relativePath)
    {
        var key = relativePath.Replace('\\', '/');
        return _templates.TryGetValue(key, out var content)
            ? content
            : throw new FileNotFoundException($"Test template '{relativePath}' was not found.");
    }

    private static string FindProjectTemplateRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var nested = Path.Combine(directory.FullName, "GigBridge", "Project_API", "Templates");
                if (Directory.Exists(nested)) return nested;

                var sibling = Path.Combine(directory.FullName, "Project_API", "Templates");
                if (Directory.Exists(sibling)) return sibling;
            }
        }

        throw new DirectoryNotFoundException("Project_API/Templates could not be located for tests.");
    }
}
