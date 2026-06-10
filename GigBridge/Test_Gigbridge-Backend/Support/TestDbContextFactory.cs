using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Support;

public static class TestDbContextFactory
{
    public static GigbridgeDbContext Create()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase($"GigBridgeTests-{Guid.NewGuid()}")
            .Options;

        return new GigbridgeDbContext(options);
    }
}
