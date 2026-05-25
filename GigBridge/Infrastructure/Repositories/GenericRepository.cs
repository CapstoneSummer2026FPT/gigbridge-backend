using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
namespace Infrastructure.Repositories;
public class GenericRepository<T> : IGenericRepository<T> where T : class {
    private readonly DbContext _context;
    private readonly DbSet<T> _dbSet;
    public GenericRepository(DbContext context) {
        _context = context;
        _dbSet = context.Set<T>();
    }
    public async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default) {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }
    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default) {
        return await _dbSet.ToListAsync(cancellationToken);
    }
    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) {
        await _dbSet.AddAsync(entity, cancellationToken);
    }
    public void Update(T entity) {
        _dbSet.Update(entity);
    }
    public void Delete(T entity) {
        _dbSet.Remove(entity);
    }
}