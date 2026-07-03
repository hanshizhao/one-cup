using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneCup.Application.Options;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.AuditLog;

public class AuditLogQueueTests
{
    private static AuditLogOptions Opts(int capacity = 1000, int batchSize = 100) => new()
    {
        QueueCapacity = capacity,
        BatchSize = batchSize,
        RetentionDays = 180,
        CleanupTime = "03:00",
    };

    private static ILogger<T> NoopLogger<T>() =>
        LoggerFactory.Create(b => { }).CreateLogger<T>();

    /// <summary>
    /// 构造测试用 ServiceProvider（注册 InMemory DbContext），
    /// 返回其 IServiceScopeFactory 供 Consumer 创建 scope 解析 DbContext。
    /// 模拟生产中"单例 BackgroundService 通过 IServiceScopeFactory 创建 scope"的用法。
    /// 用共享的内部 EF service provider，保证不同 scope 的 InMemory 同名库共享数据。
    /// </summary>
    private static (IServiceScopeFactory scopeFactory, ServiceProvider root) BuildScopeFactory(string dbName)
    {
        var efServices = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider();
        var services = new ServiceCollection();
        services.AddDbContext<OneCupDbContext>(o =>
            o.UseInMemoryDatabase(dbName).UseInternalServiceProvider(efServices));
        var root = services.BuildServiceProvider();
        return (root.GetRequiredService<IServiceScopeFactory>(), root);
    }

    /// <summary>
    /// 验证 WriteBatchAsync：一批混合 entry → 分桶 → 持久化到两张表。
    /// 直测 internal 方法而非完整 Channel 异步管道，规避消费者异步时序的 flaky（brief 退化方案）。
    /// Consumer 通过 IServiceScopeFactory 创建 scope 解析 Scoped DbContext。
    /// </summary>
    [Fact]
    public async Task WriteBatchAsync_PartitionsAndPersistsToBothTables()
    {
        // Arrange：3 条操作日志 + 2 条登录日志混在一批
        var dbName = $"audit-batch-{Guid.NewGuid()}";
        var (scopeFactory, root) = BuildScopeFactory(dbName);
        var channel = new AuditLogChannel(Opts().QueueCapacity);
        var consumer = new AuditLogQueueConsumer(
            channel, scopeFactory, Microsoft.Extensions.Options.Options.Create(Opts()), NoopLogger<AuditLogQueueConsumer>());

        var batch = new List<AuditLogEntry>
        {
            new() { Operation = new OperationLog { Module = "User", Action = "Create", Username = "a" } },
            new() { Operation = new OperationLog { Module = "Role", Action = "Update", Username = "a" } },
            new() { Operation = new OperationLog { Module = "User", Action = "Delete", Username = "a" } },
            new() { Login = new LoginLog { Username = "a", EventType = LoginEventType.Login, Result = OperationResult.Success } },
            new() { Login = new LoginLog { Username = "b", EventType = LoginEventType.Logout, Result = OperationResult.Success } },
        };

        // Act：调 internal WriteBatchAsync，确定性完成（无异步时序）
        await consumer.WriteBatchAsync(batch, CancellationToken.None);

        // Assert：分桶后 3 操作日志 + 2 登录日志各自落库（新建 scope 读，InMemory 同名库共享状态）
        using var verifyScope = scopeFactory.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<OneCupDbContext>();
        Assert.Equal(3, await db.OperationLogs.CountAsync());
        Assert.Equal(2, await db.LoginLogs.CountAsync());

        // 抽查内容正确（非仅数量）
        Assert.Contains(await db.OperationLogs.ToListAsync(),
            o => o.Module == "Role" && o.Action == "Update");
        Assert.Contains(await db.LoginLogs.ToListAsync(),
            l => l.Username == "b" && l.EventType == LoginEventType.Logout);

        channel.Dispose();
        await root.DisposeAsync();
    }

    /// <summary>
    /// 验证 DropOldest：Channel 容量满后再写，丢弃最老条目，保最近。
    /// 纯 Channel 行为，无 DB，确定性稳定。
    /// </summary>
    [Fact]
    public void Channel_DropOldest_WhenFull()
    {
        var channel = new AuditLogChannel(capacity: 3);

        // 写满 3 条
        Assert.True(channel.Writer.TryWrite(new AuditLogEntry { Login = new LoginLog { Username = "1" } }));
        Assert.True(channel.Writer.TryWrite(new AuditLogEntry { Login = new LoginLog { Username = "2" } }));
        Assert.True(channel.Writer.TryWrite(new AuditLogEntry { Login = new LoginLog { Username = "3" } }));

        // 再写一条：DropOldest 模式下 TryWrite 仍成功，丢弃最老的 "1"
        Assert.True(channel.Writer.TryWrite(new AuditLogEntry { Login = new LoginLog { Username = "4" } }));

        // 读出全部，应剩 3 条："2","3","4"，最老的 "1" 被丢弃
        var items = new List<AuditLogEntry>();
        while (channel.Reader.TryRead(out var item)) items.Add(item);

        Assert.Equal(3, items.Count);
        var usernames = items.Select(i => i.Login!.Username).ToList();
        Assert.DoesNotContain("1", usernames);
        Assert.Contains("4", usernames);

        channel.Dispose();
    }
}
