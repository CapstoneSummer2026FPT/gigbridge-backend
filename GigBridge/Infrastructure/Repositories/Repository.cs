using Application.Common.Interfaces.IRepository;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly GigbridgeDbContext _db;
        protected readonly DbSet<T> dbSet;

        public Repository(GigbridgeDbContext db)
        {
            _db = db;
            this.dbSet = _db.Set<T>();
        }

        public void Add(T entity)
        {
            dbSet.Add(entity);
        }

        public void AddRange(IEnumerable<T> entities)
        {
            dbSet.AddRange(entities);
        }

        public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await dbSet.AnyAsync(predicate, cancellationToken);
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>>? filter = null, CancellationToken cancellationToken = default)
        {
            if (filter != null)
            {
                return await dbSet.CountAsync(filter, cancellationToken);
            }
            return await dbSet.CountAsync(cancellationToken);
        }

        public T Get(Expression<Func<T, bool>> filter, string? includeProperties = null)
        {
            IQueryable<T> query = dbSet;
            if (includeProperties != null)
            {
                foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProp.Trim());
                }
            }
            return query.FirstOrDefault(filter)!;
        }

        public IEnumerable<T> GetAll(Expression<Func<T, bool>>? filter = null, string? includeProperties = null)
        {
            IQueryable<T> query = dbSet;
            if (filter != null)
            {
                query = query.Where(filter);
            }
            if (includeProperties != null)
            {
                foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProp.Trim());
                }
            }
            return query.ToList();
        }

        public async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null, string? includeProperties = null, CancellationToken cancellationToken = default)
        {
            IQueryable<T> query = dbSet;
            if (filter != null)
            {
                query = query.Where(filter);
            }
            if (includeProperties != null)
            {
                foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProp.Trim());
                }
            }
            return await query.ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<T>> GetAllPagedAsync(int pageIndex, int pageSize, Expression<Func<T, bool>>? filter = null, string? includeProperties = null, Expression<Func<T, object>>? orderBy = null, bool descending = false, CancellationToken cancellationToken = default)
        {
            IQueryable<T> query = dbSet;
            if (filter != null)
            {
                query = query.Where(filter);
            }
            if (includeProperties != null)
            {
                foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProp.Trim());
                }
            }
            if (orderBy != null)
            {
                query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
            }
            return await query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        }

        public async Task<T?> GetAsync(Expression<Func<T, bool>> filter, string? includeProperties = null, CancellationToken cancellationToken = default)
        {
            IQueryable<T> query = dbSet;
            if (includeProperties != null)
            {
                foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProp.Trim());
                }
            }
            return await query.FirstOrDefaultAsync(filter, cancellationToken);
        }

        public async Task<IEnumerable<T>> GetTopAsync(int count, Expression<Func<T, bool>>? filter = null, Expression<Func<T, object>>? orderBy = null, bool descending = false, string? includeProperties = null, CancellationToken cancellationToken = default)
        {
            IQueryable<T> query = dbSet;
            if (filter != null)
            {
                query = query.Where(filter);
            }
            if (includeProperties != null)
            {
                foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProp.Trim());
                }
            }
            if (orderBy != null)
            {
                query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
            }
            return await query.Take(count).ToListAsync(cancellationToken);
        }

        public void Remove(T entity)
        {
            dbSet.Remove(entity);
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            dbSet.RemoveRange(entities);
        }
    }
}
