using Application.Common.InternalServices.Contracts.Interfaces;
using Application.Common.InternalServices.Contracts.Milestones.Email;
using Application.Common.InternalServices.Contracts.Models;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Contracts.Milestones;

public sealed class MilestoneAttachmentPresentationTests
{
    [Theory]
    [InlineData("report.pdf", "PDF")]
    [InlineData("archive.zip", "Archive")]
    [InlineData("photo.png", "Image")]
    [InlineData("clip.mp4", "Video")]
    [InlineData("sheet.xlsx", "Excel Spreadsheet")]
    public void TypeLabel_MapsKnownExtensions(string fileName, string expectedLabel)
    {
        Assert.Equal(expectedLabel, MilestoneAttachmentPresentation.TypeLabel(fileName, null));
    }

    [Fact]
    public void TypeLabel_FallsBackToMimeTypeWhenExtensionMissing()
    {
        Assert.Equal("application/octet-stream", MilestoneAttachmentPresentation.TypeLabel("noextension", "application/octet-stream"));
    }

    [Theory]
    [InlineData(500L, "500 B")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(25_400_000L, "24.2 MB")]
    public void SizeLabel_FormatsHumanReadableUnits(long bytes, string expected)
    {
        Assert.Equal(expected, MilestoneAttachmentPresentation.SizeLabel(bytes));
    }

    [Fact]
    public void SizeLabel_ReturnsNullWhenUnavailable()
    {
        Assert.Null(MilestoneAttachmentPresentation.SizeLabel(null));
        Assert.Null(MilestoneAttachmentPresentation.SizeLabel(0));
    }
}
