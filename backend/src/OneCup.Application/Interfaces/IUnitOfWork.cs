using OneCup.Domain.Entities;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 工作单元接口,封装事务提交。
/// Infrastructure 层实现;Application 层通过它控制事务边界。
/// </summary>
public interface IUnitOfWork
{
    /// <summary>提交所有变更,返回受影响行数。</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 泛型仓储接口,提供基础的实体 CRUD 操作。
/// Infrastructure 层实现;Application 层通过它访问数据,不直接接触 EF Core。
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Remove(T entity);
}
