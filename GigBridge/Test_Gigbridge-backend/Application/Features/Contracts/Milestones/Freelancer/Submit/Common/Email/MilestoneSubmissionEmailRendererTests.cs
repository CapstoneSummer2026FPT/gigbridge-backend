using Application.Features.Contracts.Milestones.Freelancer.Submit.Common.Email;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Milestones.Freelancer.Submit.Common.Email;

public sealed class MilestoneSubmissionEmailRendererTests
{
    private static MilestoneSubmissionEmailModel BuildModel(
        IReadOnlyList<MilestoneSubmissionFileModel>? files = null) => new(
        ClientName: "Alice Client",
        JobTitle: "E-commerce Website Development",
        MilestoneTitle: "Backend Development",
        MilestoneNumber: 2,
        MilestoneCount: 5,
        StartDate: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        Deadline: new DateOnly(2026, 8, 15),
        SubmittedAt: new DateTime(2026, 8, 14, 18, 30, 0, DateTimeKind.Utc),
        StatusLabel: "Submitted",
        FreelancerName: "John Doe",
        Files: files ?? [new MilestoneSubmissionFileModel("backend-source-code.zip", "Archive", "25.4 MB", "🗜️")],
        ActionUrl: "https://app.gigbridge.example/contracts/c1/milestones/m2/approve");

    [Fact]
    public void Render_IncludesAllRequiredDynamicFields()
    {
        var renderer = new MilestoneSubmissionEmailRenderer(TestTemplateReader.FromProjectTemplates());

        var result = renderer.Render(BuildModel());

        Assert.Equal(
            "New Milestone Submission – E-commerce Website Development – Backend Development",
            result.Subject);
        Assert.Contains("Alice Client", result.HtmlBody);
        Assert.Contains("John Doe", result.HtmlBody);
        Assert.Contains("E-commerce Website Development", result.HtmlBody);
        Assert.Contains("Milestone 2 of 5", result.HtmlBody);
        Assert.Contains("Backend Development", result.HtmlBody);
        Assert.Contains("August 1, 2026", result.HtmlBody);
        Assert.Contains("August 15, 2026", result.HtmlBody);
        Assert.Contains("August 14, 2026 at 18:30 UTC", result.HtmlBody);
        Assert.Contains("Submitted", result.HtmlBody);
        Assert.Contains("backend-source-code.zip", result.HtmlBody);
        Assert.Contains("Archive", result.HtmlBody);
        Assert.Contains("25.4 MB", result.HtmlBody);
        Assert.Contains("https://app.gigbridge.example/contracts/c1/milestones/m2/approve", result.HtmlBody);
        Assert.DoesNotContain("{{", result.HtmlBody);
        Assert.Contains("backend-source-code.zip", result.TextBody);
    }

    [Fact]
    public void Render_EncodesHtmlInDynamicValues()
    {
        var renderer = new MilestoneSubmissionEmailRenderer(TestTemplateReader.FromProjectTemplates());
        var model = BuildModel() with
        {
            JobTitle = "<script>alert(1)</script>",
            Files = [new MilestoneSubmissionFileModel("<img src=x onerror=alert(1)>.zip", "Archive", "1 KB", "🗜️")]
        };

        var result = renderer.Render(model);

        Assert.DoesNotContain("<script>alert(1)</script>", result.HtmlBody);
        Assert.Contains("&lt;script&gt;", result.HtmlBody);
        Assert.DoesNotContain("<img src=x", result.HtmlBody);
    }

    [Fact]
    public void Render_WithNoFiles_ShowsNoFilesAttachedFallback()
    {
        var renderer = new MilestoneSubmissionEmailRenderer(TestTemplateReader.FromProjectTemplates());

        var result = renderer.Render(BuildModel(files: []));

        Assert.Contains("No files attached", result.HtmlBody);
        Assert.Contains("no files attached", result.TextBody);
    }

    [Fact]
    public void Render_WithMultipleFiles_ListsEveryFile()
    {
        var renderer = new MilestoneSubmissionEmailRenderer(TestTemplateReader.FromProjectTemplates());
        var files = new[]
        {
            new MilestoneSubmissionFileModel("frontend-build.zip", "Archive", "12.1 MB", "🗜️"),
            new MilestoneSubmissionFileModel("api-spec.pdf", "PDF", "340 KB", "📄"),
            new MilestoneSubmissionFileModel("demo-video.mp4", "Video", "80.2 MB", "🎬")
        };

        var result = renderer.Render(BuildModel(files));

        foreach (var file in files)
        {
            Assert.Contains(file.FileName, result.HtmlBody);
            Assert.Contains(file.FileName, result.TextBody);
        }
    }

    [Fact]
    public void Render_WithNoStartDate_UsesNotStartedFallback()
    {
        var renderer = new MilestoneSubmissionEmailRenderer(TestTemplateReader.FromProjectTemplates());
        var model = BuildModel() with { StartDate = null };

        var result = renderer.Render(model);

        Assert.Contains("Not started", result.HtmlBody);
    }
}
