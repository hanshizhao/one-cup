using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Persistence;

/// <summary>
/// UnitOfWork 事务抽象测试。
/// 注:EF InMemory provider 的事务是 no-op(无真实隔离/回滚),但能验证控制流
/// (Begin/Commit/Rollback 不抛二次异常,回调异常时回滚并重新抛出)。
/// 因此抑制 InMemoryEventId.TransactionIgnoredWarning,使事务 API 成为静默 no-op。
/// </summary>
public class UnitOfWorkTransactionTests
{
    private static (UnitOfWork uow, OneCupDbContext db) Create(string dbName)
    {
        var options = new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new OneCupDbContext(options);
        return (new UnitOfWork(db), db);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_commits_on_success()
    {
        var (uow, db) = Create(nameof(ExecuteInTransactionAsync_commits_on_success));

        var actionCalled = false;
        await uow.ExecuteInTransactionAsync(async () =>
        {
            db.Users.Add(new User { Username = "tx-user", DisplayName = "TX", Roles = new List<Role>() });
            await db.SaveChangesAsync(default);
            actionCalled = true;
        }, default);

        Assert.True(actionCalled);
        // 提交后数据落库可查
        Assert.Single(db.Users.AsEnumerable(), u => u.Username == "tx-user");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_rolls_back_and_rethrows_on_exception()
    {
        var (uow, db) = Create(nameof(ExecuteInTransactionAsync_rolls_back_and_rethrows_on_exception));
        var thrown = new InvalidOperationException("boom");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            uow.ExecuteInTransactionAsync(async () =>
            {
                db.Users.Add(new User { Username = "rollback", DisplayName = "RB", Roles = new List<Role>() });
                await db.SaveChangesAsync(default);
                throw thrown;
            }, default));

        Assert.Same(thrown, ex);
    }

    /// <summary>
    /// 关键:异常回滚后,事务不应处于"已存在但损坏"的状态——
    /// 再次调用 ExecuteInTransactionAsync 不应抛"已有事务/事务不可用"等错误。
    /// 验证控制流干净(InMemory no-op 下仍校验 BeginTransactionAsync 不抛二次异常)。
    /// </summary>
    [Fact]
    public async Task ExecuteInTransactionAsync_re_execution_after_rollback_does_not_throw()
    {
        var (uow, db) = Create(nameof(ExecuteInTransactionAsync_re_execution_after_rollback_does_not_throw));

        // 第一次:失败回滚
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            uow.ExecuteInTransactionAsync(() => throw new InvalidOperationException("first"), default));

        // 第二次:成功提交(不应报"已有事务")
        await uow.ExecuteInTransactionAsync(async () =>
        {
            db.Users.Add(new User { Username = "second", DisplayName = "2nd", Roles = new List<Role>() });
            await db.SaveChangesAsync(default);
        }, default);

        Assert.Single(db.Users.AsEnumerable(), u => u.Username == "second");
    }

    [Fact]
    public async Task BeginTransactionAsync_returns_application_transaction()
    {
        var (uow, db) = Create(nameof(BeginTransactionAsync_returns_application_transaction));
        IApplicationTransaction tx = await uow.BeginTransactionAsync(default);
        Assert.NotNull(tx);

        // 手动作用域:NumberingService 契约要求调用方持事务(CurrentTransaction 非空)
        await using (tx)
        {
            db.Users.Add(new User { Username = "manual", DisplayName = "M", Roles = new List<Role>() });
            await db.SaveChangesAsync(default);
            await tx.CommitAsync(default);
        }

        Assert.Single(db.Users.AsEnumerable(), u => u.Username == "manual");
    }

    /// <summary>
    /// 验证手动事务 Rollback 可调用且不抛二次异常(IAsyncDisposable 也不抛)。
    /// </summary>
    [Fact]
    public async Task BeginTransactionAsync_rollback_then_dispose_succeeds()
    {
        var (uow, _) = Create(nameof(BeginTransactionAsync_rollback_then_dispose_succeeds));
        var tx = await uow.BeginTransactionAsync(default);
        await tx.RollbackAsync(default);
        await tx.DisposeAsync(); // 显式 Dispose,Rollback 后不应抛
    }

    /// <summary>
    /// 逃生舱 Query():返回可查询 IQueryable,能复用 Repository 之外写 LINQ。
    /// </summary>
    [Fact]
    public async Task Repository_Query_returns_queryable_escape_hatch()
    {
        var options = new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase(nameof(Repository_Query_returns_queryable_escape_hatch)).Options;
        var db = new OneCupDbContext(options);
        db.Users.AddRange(
            new User { Username = "alice", DisplayName = "A", Roles = new List<Role>() },
            new User { Username = "bob", DisplayName = "B", Roles = new List<Role>() });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repo = new Repository<User>(db);
        var query = repo.Query();

        Assert.NotNull(query);
        // System.Linq 的 Queryable 组合可用(不依赖 EF 扩展)
        var names = query.Select(u => u.Username).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "alice", "bob" }, names);
    }
}
