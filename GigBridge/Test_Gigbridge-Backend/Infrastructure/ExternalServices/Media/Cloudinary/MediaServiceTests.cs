using Infrastructure.ExternalServices.Media.Cloudinary;

namespace Test_Gigbridge_Backend.Infrastructure.ExternalServices.Media.Cloudinary;

public sealed class MediaServiceTests
{
    [Fact]
    public void TryGetPublicId_ExtractsVersionedGigBridgeCloudinaryAsset()
    {
        var result = MediaService.TryGetPublicId(
            "https://res.cloudinary.com/demo/image/upload/v1785900000/gigbridge/portfolio/profile-id/photo.png",
            out var publicId);

        Assert.True(result);
        Assert.Equal("gigbridge/portfolio/profile-id/photo", publicId);
    }

    [Theory]
    [InlineData("https://example.com/image/upload/v1/gigbridge/portfolio/photo.png")]
    [InlineData("http://res.cloudinary.com/demo/image/upload/v1/gigbridge/portfolio/photo.png")]
    [InlineData("https://res.cloudinary.com/demo/image/upload/v1/unmanaged/photo.png")]
    public void TryGetPublicId_RejectsUnmanagedUrls(string url)
    {
        Assert.False(MediaService.TryGetPublicId(url, out _));
    }

    [Fact]
    public void IsInExpectedFolder_PreventsPortfolioCleanupFromDeletingOtherAssets()
    {
        Assert.True(MediaService.IsInExpectedFolder(
            "gigbridge/portfolio/profile-id/photo",
            "portfolio"));
        Assert.False(MediaService.IsInExpectedFolder(
            "gigbridge/signatures/contract/signature",
            "portfolio"));
    }
}
