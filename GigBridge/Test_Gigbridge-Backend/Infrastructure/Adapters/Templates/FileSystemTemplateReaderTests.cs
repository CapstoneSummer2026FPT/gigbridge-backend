using Infrastructure.Adapters.Templates;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Test_Gigbridge_Backend.Infrastructure.Adapters.Templates;

public sealed class FileSystemTemplateReaderTests
{
    [Fact]
    public async Task Reader_ReadsTextAndBinaryWithinTemplateRoot()
    {
        using var fixture = new TemplateRootFixture();
        fixture.WriteText("Auth/Email/OtpEmail.html", "hello-template");
        fixture.WriteBytes("ESign/Documents/template.docx", [1, 2, 3, 4]);
        var reader = fixture.CreateReader();

        Assert.Equal("hello-template", reader.ReadText("Auth/Email/OtpEmail.html"));
        Assert.Equal(
            "hello-template",
            await reader.ReadTextAsync("Auth/Email/OtpEmail.html"));
        await using var stream = reader.OpenRead("ESign/Documents/template.docx");
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        Assert.Equal([1, 2, 3, 4], output.ToArray());
    }

    [Fact]
    public void Reader_RejectsMissingTemplate()
    {
        using var fixture = new TemplateRootFixture();
        var reader = fixture.CreateReader();

        Assert.Throws<FileNotFoundException>(() => reader.ReadText("missing.html"));
    }

    [Fact]
    public void Reader_RejectsRootedPath()
    {
        using var fixture = new TemplateRootFixture();
        var reader = fixture.CreateReader();
        var rootedPath = Path.Combine(Path.GetPathRoot(fixture.RootPath)!, "outside.html");

        Assert.Throws<ArgumentException>(() => reader.ReadText(rootedPath));
    }

    [Fact]
    public void Reader_RejectsParentTraversal()
    {
        using var fixture = new TemplateRootFixture();
        var reader = fixture.CreateReader();

        Assert.Throws<ArgumentException>(() => reader.ReadText("../outside.html"));
    }

    private sealed class TemplateRootFixture : IDisposable
    {
        internal TemplateRootFixture()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "gigbridge-template-reader-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(RootPath, "Templates"));
        }

        internal string RootPath { get; }

        internal FileSystemTemplateReader CreateReader()
        {
            var environment = Substitute.For<IHostEnvironment>();
            environment.ContentRootPath.Returns(RootPath);
            return new FileSystemTemplateReader(environment);
        }

        internal void WriteText(string relativePath, string content)
        {
            var path = PreparePath(relativePath);
            File.WriteAllText(path, content);
        }

        internal void WriteBytes(string relativePath, byte[] content)
        {
            var path = PreparePath(relativePath);
            File.WriteAllBytes(path, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath)) Directory.Delete(RootPath, recursive: true);
        }

        private string PreparePath(string relativePath)
        {
            var path = Path.Combine(RootPath, "Templates", relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return path;
        }
    }
}
