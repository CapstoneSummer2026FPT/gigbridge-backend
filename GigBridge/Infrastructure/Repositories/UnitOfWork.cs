using Application.Common.Interfaces.IRepository;
using Infrastructure.Persistence;

namespace Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly GigbridgeDbContext _context;

    public IUserRepository UserRepository { get; private set; }


    public UnitOfWork(GigbridgeDbContext context)
    {
        _context = context;
        UserRepository = new UserRepository(_context);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
