using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OneCup.Application.Interfaces;

namespace OneCup.Infrastructure.Persistence;

/// <summary>
/// 工作单元实现,封装 DbContext.SaveChangesAsync 及事务边界。
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly OneCupDbContext _context;

    public UnitOfWork(OneCupDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        // EF Core 会在环境事务内自动为各 SaveChanges 建立 savepoint,使嵌套保存可重试。
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            await action();
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => new EfApplicationTransaction(await _context.Database.BeginTransactionAsync(ct));

    /// <summary>
    /// 将 EF Core 的 IDbContextTransaction 适配为应用层 IApplicationTransaction,
    /// 不向 Application 层泄漏 EF 类型。
    /// Dispose 委托给底层事务(EF 的 Dispose 在未 Commit 时等价于 Rollback,幂等安全)。
    /// </summary>
    private sealed class EfApplicationTransaction : IApplicationTransaction
    {
        private readonly IDbContextTransaction _tx;
        private bool _disposed;

        public EfApplicationTransaction(IDbContextTransaction tx) => _tx = tx;

        public Task CommitAsync(CancellationToken ct = default) => _tx.CommitAsync(ct);

        public Task RollbackAsync(CancellationToken ct = default) => _tx.RollbackAsync(ct);

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            await _tx.DisposeAsync();
        }
    }
}
