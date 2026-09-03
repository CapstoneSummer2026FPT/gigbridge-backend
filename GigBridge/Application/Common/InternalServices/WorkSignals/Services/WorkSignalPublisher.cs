using Application.Common.Interfaces;
using Application.Common.InternalServices.WorkSignals.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.InternalServices.WorkSignals.Services;

internal sealed class WorkSignalPublisher(IApplicationDbContext context) : IWorkSignalPublisher
{
    public async Task PublishAsync(string channel, CancellationToken cancellationToken)
    {
        var dbContext = (DbContext)context;
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_notify({channel}, '')",
            cancellationToken);
    }
}
