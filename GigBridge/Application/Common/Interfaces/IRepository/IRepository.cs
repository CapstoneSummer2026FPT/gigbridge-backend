using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Interfaces.IRepository
{
    public interface IRepository<T>  where T : class
    {
        Task<T?> GetAsync(Expression<Func<T, bool>> filter, string? includeProperties = null);

        Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null, string? includeProperties = null);

        Task<IEnumerable<T>> GetAllPagedAsync(int pageIndex, int pageSize, Expression<Func<T, bool>>? filter = null, string? includeProperties = null, Expression<Func<T, object>>? orderBy = null, bool descending = false);

        Task<IEnumerable<T>> GetTopAsync(int count, Expression<Func<T, bool>>? filter = null, Expression<Func<T, object>>? orderBy = null, bool descending = false, string? includeProperties = null);

        //Func<object, object> value
        Task<int> CountAsync(Expression<Func<T, bool>>? filter = null);

        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);


        //=========================================================================================================
        T Get(Expression<Func<T, bool>> filter, string? includeProperties = null);
        IEnumerable<T> GetAll(Expression<Func<T, bool>>? filter = null, string? includeProperties = null);
        void Add(T entity);

        void AddRange(IEnumerable<T> entities);

        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);
        //=========================================================================================================
    }
}
