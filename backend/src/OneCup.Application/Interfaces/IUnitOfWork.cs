using OneCup.Application.Specifications;
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

    /// <summary>
    /// 在事务中执行操作,自动提交(回调正常返回)或回滚(回调抛异常)。
    /// EF Core 会在环境事务内自动为各 SaveChanges 建立 savepoint,使嵌套保存可重试。
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);

    /// <summary>
    /// 手动开启一个事务,返回应用层事务句柄(调用方负责 Commit/Rollback/Dispose)。
    /// 供调用方控制事务作用域(跨多个 Application Service 调用)。
    /// 注意:优先使用 ExecuteInTransactionAsync;仅在需要手动控制边界(如 NumberingService
    /// 要求调用方持有事务)时使用本方法。
    /// </summary>
    Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

/// <summary>
/// 泛型仓储接口,提供基础的实体 CRUD 操作。
/// Infrastructure 层实现;Application 层通过它访问数据,不直接接触 EF Core。
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default);

    Task<int> CountAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// 绕过全局 QueryFilter(如软删除过滤器)判断是否存在匹配实体。
    /// 用于唯一性预检:已软删除记录占用的唯一值(如用户名)仍需被识别为"已占用",
    /// 以便返回清晰的 400 而非触发数据库唯一索引冲突(500)。
    /// EF 的 IgnoreQueryFilters 封装在 Infrastructure 实现内,Application 层不感知 EF。
    /// </summary>
    Task<bool> AnyIgnoringFiltersAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Remove(T entity);

    /// <summary>
    /// 逃生舱:返回实体的可查询 IQueryable,供 Specification 无法表达的查询使用
    /// (左连接、聚合投影、GroupBy 等)。
    /// 注意:IQueryable&lt;T&gt; 来自 System.Linq,不引入 EF Core 耦合;
    /// 但调用方若使用 EF 扩展方法(Include/ToListAsync 等)则需自行确保可测试性。
    /// </summary>
    IQueryable<T> Query();
}
