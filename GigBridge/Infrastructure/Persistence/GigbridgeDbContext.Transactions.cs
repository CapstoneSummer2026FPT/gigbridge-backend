using Application.Common.Interfaces;

namespace Infrastructure.Persistence;

public partial class GigbridgeDbContext
{
    public async Task<IApplicationDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new EfApplicationDbContextTransaction(transaction);
    }
}
