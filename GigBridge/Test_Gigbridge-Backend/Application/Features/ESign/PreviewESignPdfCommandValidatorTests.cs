using Application.Features.ESign.Common.PreviewPdf.Commands;
using Application.Features.ESign.Common.PreviewPdf.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.ESign;

public sealed class PreviewESignPdfCommandValidatorTests
{
    private readonly PreviewESignPdfCommandValidator _validator = new();

    [Fact]
    public void Validate_AcceptsCroppedSignatureDimensions()
    {
        var command = CreateCommand(width: 55, height: 80);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 80)]
    [InlineData(55, 0)]
    [InlineData(1201, 80)]
    [InlineData(55, 501)]
    public void Validate_RejectsDimensionsOutsideSupportedRange(int width, int height)
    {
        var command = CreateCommand(width, height);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }

    private static PreviewESignPdfCommand CreateCommand(int width, int height) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new PreviewESignPdfRequest(
                "data:image/png;base64,AA==",
                width,
                height,
                "999999999"),
            "127.0.0.1",
            "test");
}
