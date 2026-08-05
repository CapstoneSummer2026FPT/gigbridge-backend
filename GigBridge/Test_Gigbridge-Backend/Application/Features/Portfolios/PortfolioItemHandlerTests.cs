using Application.Common.Exceptions;
using Application.Features.Portfolios.Common.DTOs;
using Application.Features.Portfolios.CreatePortfolioItem.Commands;
using Application.Features.Portfolios.DeletePortfolioItem.Commands;
using Application.Features.Portfolios.GetPortfolioItems.Queries;
using Application.Features.Portfolios.UpdatePortfolioItem.Commands;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Portfolios;

public sealed class PortfolioItemHandlerTests
{
    [Fact]
    public async Task Handlers_CreateReadUpdateAndDeleteAnOwnedPortfolioItem()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var profile = AddFreelancer(context, userId);
        await context.SaveChangesAsync();
        const string firstImageUrl =
            "https://res.cloudinary.com/gigbridge/image/upload/v1/gigbridge/portfolio/profile/first.png";
        const string secondImageUrl =
            "https://res.cloudinary.com/gigbridge/image/upload/v2/gigbridge/portfolio/profile/second.png";
        var mediaService = new FakeMediaService(firstImageUrl, secondImageUrl);

        var created = await new CreatePortfolioItemCommandHandler(
            context,
            mediaService,
            NullLogger<CreatePortfolioItemCommandHandler>.Instance).Handle(
            new CreatePortfolioItemCommand(userId, new PortfolioItemInputDto
            {
                Title = " First project ",
                Description = " Initial description ",
                ProjectUrl = " https://example.com/first ",
                ProjectDate = new DateOnly(2026, 8, 1)
            }, CreatePngUpload("first.png")),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.PortfolioItemId);
        Assert.Equal("First project", created.Title);
        Assert.Equal(firstImageUrl, created.ImageUrl);
        Assert.Equal(profile.FreelancerProfilesId,
            (await context.PortfolioItems.SingleAsync()).FreelancerId);
        Assert.Equal($"portfolio/{profile.FreelancerProfilesId}",
            Assert.Single(mediaService.Uploads).Folder);

        var listed = await new GetPortfolioItemsQueryHandler(context).Handle(
            new GetPortfolioItemsQuery(userId),
            CancellationToken.None);

        Assert.Equal(created.PortfolioItemId, Assert.Single(listed).PortfolioItemId);
        Assert.Equal("2026-08-01", listed[0].ProjectDate);

        var updateHandler = new UpdatePortfolioItemCommandHandler(
            context,
            mediaService,
            NullLogger<UpdatePortfolioItemCommandHandler>.Instance);
        var metadataUpdate = await updateHandler.Handle(
            new UpdatePortfolioItemCommand(userId, created.PortfolioItemId, new PortfolioItemInputDto
            {
                Title = "Updated project",
                ProjectDate = new DateOnly(2026, 8, 2)
            }, PreserveExistingImage: true),
            CancellationToken.None);

        Assert.Equal(firstImageUrl, metadataUpdate.ImageUrl);
        Assert.Empty(mediaService.DeletedFiles);

        var updated = await updateHandler.Handle(
            new UpdatePortfolioItemCommand(userId, created.PortfolioItemId, new PortfolioItemInputDto
            {
                Title = "Updated project",
                ProjectDate = new DateOnly(2026, 8, 2)
            }, CreatePngUpload("second.png")),
            CancellationToken.None);

        Assert.Equal("Updated project", updated.Title);
        Assert.Equal("2026-08-02", updated.ProjectDate);
        Assert.Equal(secondImageUrl, updated.ImageUrl);
        Assert.Contains(firstImageUrl, mediaService.DeletedFiles);

        var deleted = await new DeletePortfolioItemCommandHandler(
            context,
            mediaService,
            NullLogger<DeletePortfolioItemCommandHandler>.Instance).Handle(
            new DeletePortfolioItemCommand(userId, created.PortfolioItemId),
            CancellationToken.None);

        Assert.True(deleted);
        Assert.Empty(context.PortfolioItems);
        Assert.Contains(secondImageUrl, mediaService.DeletedFiles);
    }

    [Fact]
    public async Task Update_RejectsAnItemOwnedByAnotherFreelancer()
    {
        await using var context = CreateContext();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var ownerProfile = AddFreelancer(context, ownerId);
        AddFreelancer(context, otherUserId);
        var item = new PortfolioItem
        {
            PortfolioItemsId = Guid.NewGuid(),
            FreelancerId = ownerProfile.FreelancerProfilesId,
            Title = "Owner project"
        };
        context.PortfolioItems.Add(item);
        await context.SaveChangesAsync();

        var action = () => new UpdatePortfolioItemCommandHandler(
            context,
            new FakeMediaService(),
            NullLogger<UpdatePortfolioItemCommandHandler>.Instance).Handle(
            new UpdatePortfolioItemCommand(otherUserId, item.PortfolioItemsId, new PortfolioItemInputDto
            {
                Title = "Unauthorized update"
            }),
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
        Assert.Equal("Owner project", item.Title);
    }

    [Fact]
    public void Validators_RejectInvalidProjectUrlAndMissingTitle()
    {
        var dto = new PortfolioItemInputDto
        {
            Title = "",
            ProjectUrl = "javascript:alert('xss')"
        };

        var createResult = new CreatePortfolioItemCommandValidator().Validate(
            new CreatePortfolioItemCommand(Guid.NewGuid(), dto));
        var updateResult = new UpdatePortfolioItemCommandValidator().Validate(
            new UpdatePortfolioItemCommand(Guid.NewGuid(), Guid.NewGuid(), dto));

        Assert.False(createResult.IsValid);
        Assert.False(updateResult.IsValid);
        Assert.Contains(createResult.Errors, error => error.PropertyName.EndsWith("Title"));
        Assert.Contains(createResult.Errors, error => error.PropertyName.EndsWith("ProjectUrl"));
    }

    [Fact]
    public async Task Create_RejectsAFileWhoseContentDoesNotMatchItsImageType()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        AddFreelancer(context, userId);
        await context.SaveChangesAsync();
        var mediaService = new FakeMediaService();
        var invalidContent = new byte[] { 0x4E, 0x4F, 0x54, 0x50, 0x4E, 0x47 };

        var action = () => new CreatePortfolioItemCommandHandler(
            context,
            mediaService,
            NullLogger<CreatePortfolioItemCommandHandler>.Instance).Handle(
            new CreatePortfolioItemCommand(userId, new PortfolioItemInputDto
            {
                Title = "Invalid image"
            }, new PortfolioImageUpload(
                new MemoryStream(invalidContent),
                "invalid.png",
                "image/png",
                invalidContent.Length)),
            CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(action);
        Assert.Empty(mediaService.Uploads);
        Assert.Empty(context.PortfolioItems);
    }

    private static GigbridgeDbContext CreateContext() => new(
        new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FreelancerProfile AddFreelancer(GigbridgeDbContext context, Guid userId)
    {
        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        context.FreelancerProfiles.Add(profile);
        return profile;
    }

    private static PortfolioImageUpload CreatePngUpload(string fileName)
    {
        var content = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x00
        };
        return new PortfolioImageUpload(
            new MemoryStream(content),
            fileName,
            "image/png",
            content.Length);
    }
}
