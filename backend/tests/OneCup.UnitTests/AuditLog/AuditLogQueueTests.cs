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
    /// 测试用 DbContext 工厂：每次 Create 返回一个新的 InMemory DbContext。
    /// 模拟生产中"消费者通过 factory 创建短生命周期 DbContext"的用法（WriteBatchAsync
    /// 用 await using 释放它）。InMemory 同名库天然共享状态，因此新实例仍能读到上次的写入。
    /// </summary>
    private sealed class TestDbFactory : IDbContextFactory<OneCupDbContext>
    {
        private readonly DbContextOptions<OneCupDbContext> _options;

        public TestDbFactory(string dbName)
        {
            var sp = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();
            _options = new DbContextOptionsBuilder<OneCupDbContext>()
                .UseInMemoryDatabase(dbName)
                .UseInternalServiceProvider(sp)
                .Options;
        }

        public OneCupDbContext CreateDbContext() => new(_options);

        public Task<OneCupDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new OneCupDbContext(_options));
    }

    /// <summary>
    /// 验证 WriteBatchAsync：一批混合 entry → 分桶 → 持久化到两张表。
    /// 直测 internal 方法而非完整 Channel 异步管道，规避 InMemory 下构造
    /// IDbContextFactory 的繁琐与消费者异步时序的 flaky（brief 退化方案）。
    /// </summary>
    [Fact]
    public async Task WriteBatchAsync_PartitionsAndPersistsToBothTables()
    {
        // Arrange：3 条操作日志 + 2 条登录日志混在一批
        var factory = new TestDbFactory($"audit-batch-{Guid.NewGuid()}");
        var channel = new AuditLogChannel(Opts().QueueCapacity);
        var consumer = new AuditLogQueueConsumer(
            channel, factory, Microsoft.Extensions.Options.Options.Create(Opts()), NoopLogger<AuditLogQueueConsumer>());

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

        // Assert：分桶后 3 操作日志 + 2 登录日志各自落库
        var db = factory.CreateDbContext();
        Assert.Equal(3, await db.OperationLogs.CountAsync());
        Assert.Equal(2, await db.LoginLogs.CountAsync());

        // 抽查内容正确（非仅数量）
        Assert.Contains(await db.OperationLogs.ToListAsync(),
            o => o.Module == "Role" && o.Action == "Update");
        Assert.Contains(await db.LoginLogs.ToListAsync(),
            l => l.Username == "b" && l.EventType == LoginEventType.Logout);

        channel.Dispose();
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
