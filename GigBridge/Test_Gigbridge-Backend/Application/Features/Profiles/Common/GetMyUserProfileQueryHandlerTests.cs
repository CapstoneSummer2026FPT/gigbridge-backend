using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Profiles.Common.DTOs;
using Application.Features.Profiles.Common.GetMyUserProfile.Queries;
using Application.Features.Profiles.Common.GetUserProfile.Queries;
using MediatR;
using NSubstitute;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Common;

public sealed class GetMyUserProfileQueryHandlerTests
{
    [Fact]
    public async Task Handle_UsesAuthenticatedUserId()
    {
        var userId = Guid.NewGuid();
        var expected = new UserProfileDto
        {
            UserId = userId,
            FullName = "Current User",
            Email = "current@example.com",
            Role = 0
        };
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetUserProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);
        var handler = new GetMyUserProfileQueryHandler(
            new FixedCurrentUserService(userId.ToString()),
            mediator);

        var result = await handler.Handle(new GetMyUserProfileQuery(), CancellationToken.None);

        Assert.Same(expected, result);
        await mediator.Received(1).Send(
            Arg.Is<GetUserProfileQuery>(query => query.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ThrowsBadRequestForInvalidTokenUserId()
    {
        var handler = new GetMyUserProfileQueryHandler(
            new FixedCurrentUserService("not-a-guid"),
            Substitute.For<IMediator>());

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new GetMyUserProfileQuery(),
            CancellationToken.None));
    }

    private sealed class FixedCurrentUserService : ICurrentUserService
    {
        public FixedCurrentUserService(string? userId)
        {
            UserId = userId;
        }

        public string? UserId { get; }
        public string? Email => null;
        public string? Role => null;
    }
}
