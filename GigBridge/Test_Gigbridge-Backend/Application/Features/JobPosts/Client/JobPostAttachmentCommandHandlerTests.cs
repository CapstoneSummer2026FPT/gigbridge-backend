using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.Attachments.Commands;
using Domain.Entities;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public sealed class JobPostAttachmentCommandHandlerTests
{
    private static readonly byte[] ValidPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D
    ];

    [Fact]
    public async Task Upload_ValidPng_PersistsJobPostAttachment()
    {
        var fixture = CreateFixture();
        var media = new FakeMediaService("https://files.example/project.png");
        var now = new DateTime(2026, 7, 27, 8, 0, 0, DateTimeKind.Utc);
        var handler = new UploadJobPostAttachmentCommandHandler(
            fixture.Context,
            media,
            new FixedDateTimeService(now));

        var result = await handler.Handle(new UploadJobPostAttachmentCommand(
            fixture.JobPostId,
            fixture.UserId,
            new MemoryStream(ValidPng),
            "project.png",
            "image/png",
            ValidPng.Length), CancellationToken.None);

        var attachment = Assert.Single(fixture.Attachments.Entities);
        Assert.Equal(result.JobPostAttachmentsId, attachment.JobPostAttachmentsId);
        Assert.Equal("project.png", attachment.FileName);
        Assert.Equal("https://files.example/project.png", attachment.FileUrl);
        Assert.Equal(now, attachment.CreatedAt);
        Assert.Equal($"job-posts/{fixture.JobPostId}/attachments", Assert.Single(media.Uploads).Folder);
    }

    [Fact]
    public async Task Upload_NonImageMimeType_IsRejectedBeforeStorage()
    {
        var fixture = CreateFixture();
        var media = new FakeMediaService();
        var handler = new UploadJobPostAttachmentCommandHandler(
            fixture.Context,
            media,
            new FixedDateTimeService(DateTime.UtcNow));

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UploadJobPostAttachmentCommand(
                fixture.JobPostId,
                fixture.UserId,
                new MemoryStream(ValidPng),
                "project.pdf",
                "application/pdf",
                ValidPng.Length),
            CancellationToken.None));

        Assert.Empty(media.Uploads);
        Assert.Empty(fixture.Attachments.Entities);
    }

    [Fact]
    public async Task Upload_SpoofedImageContent_IsRejectedBeforeStorage()
    {
        var fixture = CreateFixture();
        var media = new FakeMediaService();
        var handler = new UploadJobPostAttachmentCommandHandler(
            fixture.Context,
            media,
            new FixedDateTimeService(DateTime.UtcNow));

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UploadJobPostAttachmentCommand(
                fixture.JobPostId,
                fixture.UserId,
                new MemoryStream("not-an-image"u8.ToArray()),
                "project.png",
                "image/png",
                12),
            CancellationToken.None));

        Assert.Empty(media.Uploads);
        Assert.Empty(fixture.Attachments.Entities);
    }

    private static Fixture CreateFixture()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        context.AddSet(new ClientProfile
        {
            ClientProfilesId = clientProfileId,
            UserId = userId
        });
        context.AddSet(new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = "Draft",
            Description = string.Empty,
            Status = 0,
            Visibility = 0
        });
        var attachments = context.AddSet<JobPostAttachment>();
        return new Fixture(context, attachments, userId, jobPostId);
    }

    private sealed record Fixture(
        InMemoryApplicationDbContext Context,
        TestDbSet<JobPostAttachment> Attachments,
        Guid UserId,
        Guid JobPostId);

    private sealed class FixedDateTimeService(DateTime now) : IDateTimeService
    {
        public DateTime UtcNow => now;
    }
}
