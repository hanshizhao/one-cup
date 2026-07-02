namespace OneCup.Application.Interfaces;

/// <summary>
/// 应用层事务抽象,不泄漏 EF Core 的 IDbContextTransaction。
/// Application 层通过 IUnitOfWork.BeginTransactionAsync / ExecuteInTransactionAsync 获取。
/// </summary>
public interface IApplicationTransaction : IAsyncDisposable
{
    /// <summary>提交事务。</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>回滚事务。</summary>
    Task RollbackAsync(CancellationToken ct = default);
}
