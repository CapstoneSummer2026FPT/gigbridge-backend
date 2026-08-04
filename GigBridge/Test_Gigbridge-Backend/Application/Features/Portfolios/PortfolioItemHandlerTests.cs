using Application.Common.Exceptions;
using Application.Features.Portfolios.Common.DTOs;
using Application.Features.Portfolios.CreatePortfolioItem.Commands;
using Application.Features.Portfolios.DeletePortfolioItem.Commands;
using Application.Features.Portfolios.GetPortfolioItems.Queries;
using Application.Features.Portfolios.UpdatePortfolioItem.Commands;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

        var created = await new CreatePortfolioItemCommandHandler(context).Handle(
            new CreatePortfolioItemCommand(userId, new PortfolioItemInputDto
            {
                Title = " First project ",
                Description = " Initial description ",
                ProjectUrl = " https://example.com/first ",
                ImageUrl = " https://example.com/first.png ",
                ProjectDate = new DateOnly(2026, 8, 1)
            }),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.PortfolioItemId);
        Assert.Equal("First project", created.Title);
        Assert.Equal(profile.FreelancerProfilesId,
            (await context.PortfolioItems.SingleAsync()).FreelancerId);

        var listed = await new GetPortfolioItemsQueryHandler(context).Handle(
            new GetPortfolioItemsQuery(userId),
            CancellationToken.None);

        Assert.Equal(created.PortfolioItemId, Assert.Single(listed).PortfolioItemId);
        Assert.Equal("2026-08-01", listed[0].ProjectDate);

        var updated = await new UpdatePortfolioItemCommandHandler(context).Handle(
            new UpdatePortfolioItemCommand(userId, created.PortfolioItemId, new PortfolioItemInputDto
            {
                Title = "Updated project",
                ProjectDate = new DateOnly(2026, 8, 2)
            }),
            CancellationToken.None);

        Assert.Equal("Updated project", updated.Title);
        Assert.Equal("2026-08-02", updated.ProjectDate);

        var deleted = await new DeletePortfolioItemCommandHandler(context).Handle(
            new DeletePortfolioItemCommand(userId, created.PortfolioItemId),
            CancellationToken.None);

        Assert.True(deleted);
        Assert.Empty(context.PortfolioItems);
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

        var action = () => new UpdatePortfolioItemCommandHandler(context).Handle(
            new UpdatePortfolioItemCommand(otherUserId, item.PortfolioItemsId, new PortfolioItemInputDto
            {
                Title = "Unauthorized update"
            }),
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(action);
        Assert.Equal("Owner project", item.Title);
    }

    [Fact]
    public void Validators_RejectInvalidUrlsAndMissingTitle()
    {
        var dto = new PortfolioItemInputDto
        {
            Title = "",
            ProjectUrl = "javascript:alert('xss')",
            ImageUrl = "not-a-url"
        };

        var createResult = new CreatePortfolioItemCommandValidator().Validate(
            new CreatePortfolioItemCommand(Guid.NewGuid(), dto));
        var updateResult = new UpdatePortfolioItemCommandValidator().Validate(
            new UpdatePortfolioItemCommand(Guid.NewGuid(), Guid.NewGuid(), dto));

        Assert.False(createResult.IsValid);
        Assert.False(updateResult.IsValid);
        Assert.Contains(createResult.Errors, error => error.PropertyName.EndsWith("Title"));
        Assert.Contains(createResult.Errors, error => error.PropertyName.EndsWith("ProjectUrl"));
        Assert.Contains(createResult.Errors, error => error.PropertyName.EndsWith("ImageUrl"));
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
}
