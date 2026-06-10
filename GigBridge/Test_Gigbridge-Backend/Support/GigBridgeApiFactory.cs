using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Test_Gigbridge_Backend.Support;

public sealed class GigBridgeApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"GigBridgeApiTests-{Guid.NewGuid()}";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<GigbridgeDbContext>>();
            services.RemoveAll<GigbridgeDbContext>();
            services.RemoveAll<IApplicationDbContext>();
            services.RemoveAll<ICacheService>();

            services.AddDbContext<GigbridgeDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddScoped<IApplicationDbContext>(provider =>
                provider.GetRequiredService<GigbridgeDbContext>());
            services.AddSingleton<ICacheService, FakeCacheService>();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                    options.DefaultChallengeScheme = TestAuthHandler.Scheme;
                    options.DefaultScheme = TestAuthHandler.Scheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.Scheme,
                    _ => { });
        });
    }

    public HttpClient CreateAuthenticatedClient(Guid userId, string role)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        return client;
    }

    public async Task SeedAsync(Func<GigbridgeDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GigbridgeDbContext>();
        await seed(context);
        await context.SaveChangesAsync();
    }
}
