using System.Collections;
using System.Linq.Expressions;
using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace Test_Gigbridge_Backend.TestSupport;

internal sealed class InMemoryApplicationDbContext : IApplicationDbContext
{
    private readonly Dictionary<Type, object> _sets = new();

    public TestDbSet<TEntity> AddSet<TEntity>(params TEntity[] entities)
        where TEntity : class
    {
        var set = new TestDbSet<TEntity>(entities);
        _sets[typeof(TEntity)] = set;
        return set;
    }

    public DbSet<TEntity> Set<TEntity>()
        where TEntity : class
    {
        if (_sets.TryGetValue(typeof(TEntity), out var set))
        {
            return (DbSet<TEntity>)set;
        }

        return AddSet<TEntity>();
    }

    public int SaveChangesCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCount++;
        return Task.FromResult(1);
    }
}

internal sealed class TestDbSet<TEntity> : DbSet<TEntity>, IQueryable<TEntity>, IAsyncEnumerable<TEntity>
    where TEntity : class
{
    private readonly List<TEntity> _entities;
    private readonly IQueryable<TEntity> _queryable;

    public TestDbSet(IEnumerable<TEntity> entities)
    {
        _entities = entities.ToList();
        _queryable = _entities.AsQueryable();
    }

    public IReadOnlyList<TEntity> Entities => _entities;

    public override IEntityType EntityType => null!;

    public override EntityEntry<TEntity> Add(TEntity entity)
    {
        _entities.Add(entity);
        return null!;
    }

    public override ValueTask<EntityEntry<TEntity>> AddAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        _entities.Add(entity);
        return ValueTask.FromResult<EntityEntry<TEntity>>(null!);
    }

    public override IAsyncEnumerator<TEntity> GetAsyncEnumerator(
        CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<TEntity>(_entities.GetEnumerator());
    }

    Type IQueryable.ElementType => _queryable.ElementType;

    Expression IQueryable.Expression => _queryable.Expression;

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<TEntity>(_queryable.Provider);

    IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator()
    {
        return _entities.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _entities.GetEnumerator();
    }
}

internal sealed class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    public TestAsyncQueryProvider(IQueryProvider inner)
    {
        _inner = inner;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        return new TestAsyncEnumerable<TEntity>(expression);
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new TestAsyncEnumerable<TElement>(expression);
    }

    public object? Execute(Expression expression)
    {
        return _inner.Execute(expression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return _inner.Execute<TResult>(expression);
    }

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
    {
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethod(nameof(IQueryProvider.Execute), 1, new[] { typeof(Expression) })!
            .MakeGenericMethod(expectedResultType)
            .Invoke(_inner, new object[] { expression });

        return (TResult)typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(expectedResultType)
            .Invoke(null, new[] { executionResult })!;
    }
}

internal sealed class TestAsyncEnumerable<T> :
    EnumerableQuery<T>,
    IAsyncEnumerable<T>,
    IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable)
        : base(enumerable)
    {
    }

    public TestAsyncEnumerable(Expression expression)
        : base(expression)
    {
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal sealed class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner)
    {
        _inner = inner;
    }

    public T Current => _inner.Current;

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        return ValueTask.FromResult(_inner.MoveNext());
    }
}
