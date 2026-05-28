namespace Application.Common.Interfaces.IRepository;

public interface IUnitOfWork
{
    // Add repository properties here
    public IUserRepository UserRepository { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
