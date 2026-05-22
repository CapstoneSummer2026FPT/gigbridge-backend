using Application.Common.Interfaces.IRepository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        public void Add(T entity)
        {
            throw new NotImplementedException();
        }

        public void AddRange(IEnumerable<T> entities)
        {
            throw new NotImplementedException();
        }

        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            throw new NotImplementedException();
        }

        public Task<int> CountAsync(Expression<Func<T, bool>>? filter = null)
        {
            throw new NotImplementedException();
        }

        public T Get(Expression<Func<T, bool>> filter, string? includeProperties = null)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> GetAll(Expression<Func<T, bool>>? filter = null, string? includeProperties = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null, string? includeProperties = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetAllPagedAsync(int pageIndex, int pageSize, Expression<Func<T, bool>>? filter = null, string? includeProperties = null, Expression<Func<T, object>>? orderBy = null, bool descending = false)
        {
            throw new NotImplementedException();
        }

        public Task<T?> GetAsync(Expression<Func<T, bool>> filter, string? includeProperties = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetTopAsync(int count, Expression<Func<T, bool>>? filter = null, Expression<Func<T, object>>? orderBy = null, bool descending = false, string? includeProperties = null)
        {
            throw new NotImplementedException();
        }

        public void Remove(T entity)
        {
            throw new NotImplementedException();
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            throw new NotImplementedException();
        }
    }
}
