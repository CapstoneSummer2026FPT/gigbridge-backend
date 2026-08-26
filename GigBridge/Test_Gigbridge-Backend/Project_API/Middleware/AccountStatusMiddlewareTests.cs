using System.Security.Claims;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.Interfaces.Caching;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Project_API.Middleware;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Project_API.Middleware;

public sealed class AccountStatusMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SignalRNegotiate_SkipsDuplicateAccountLookup()
    {
        var nextCalled = false;
        var context = CreateAuthenticatedContext(Guid.NewGuid());
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/hubs/notification/negotiate";
        var database = Substitute.For<IApplicationDbContext>();
        var middleware = new AccountStatusMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            database,
            Substitute.For<IDateTimeService>(),
            Substitute.For<ICacheService>(),
            NullLogger<AccountStatusMiddleware>.Instance);

        Assert.True(nextCalled);
        database.DidNotReceive().Set<User>();
    }

    [Fact]
    public async Task InvokeAsync_SignalRTransport_StillChecksAccountStatus()
    {
        var userId = Guid.NewGuid();
        var context = CreateAuthenticatedContext(userId);
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/hubs/notification";
        var database = new InMemoryApplicationDbContext();
        database.AddSet(new User
        {
            UserId = userId,
            Email = "benchmark@example.com",
            FullName = "Benchmark User",
            IsActive = true
        });
        var nextCalled = false;
        var middleware = new AccountStatusMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            database,
            Substitute.For<IDateTimeService>(),
            Substitute.For<ICacheService>(),
            NullLogger<AccountStatusMiddleware>.Instance);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenCacheIsUnavailable_FallsBackToDatabase()
    {
        var userId = Guid.NewGuid();
        var context = CreateAuthenticatedContext(userId);
        var database = new InMemoryApplicationDbContext();
        database.AddSet(new User
        {
            UserId = userId,
            Email = "cache-fallback@example.com",
            FullName = "Cache Fallback",
            IsActive = true
        });
        var nextCalled = false;
        var middleware = new AccountStatusMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            database,
            Substitute.For<IDateTimeService>(),
            new ThrowingCacheService(),
            NullLogger<AccountStatusMiddleware>.Instance);

        Assert.True(nextCalled);
    }

    private static DefaultHttpContext CreateAuthenticatedContext(Guid userId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "Test"));
        return context;
    }

    private sealed class ThrowingCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromException<T?>(new InvalidOperationException("Redis unavailable"));

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("Redis unavailable"));

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("Redis unavailable"));
    }
}
