using Microsoft.EntityFrameworkCore;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using OneCup.Infrastructure.Services;
using Testcontainers.PostgreSql;

namespace OneCup.UnitTests.Numbering;

/// <summary>
/// 并发取号测试。必须用真实 PostgreSQL（Testcontainers）——InMemory 不支持 FOR UPDATE 行锁。
/// 运行需要本机可访问 Docker。
/// </summary>
public class NumberingServiceConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("numbering_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private OneCupDbContext _db = null!;
    private NumberingService _svc = null!;
    private Guid _ruleId;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        var options = new DbContextOptionsBuilder<OneCupDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .Options;
        _db = new OneCupDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var rule = new NumberingRule
        {
            TargetType = "fabric",
            Name = "面料",
            Prefix = "FAB",
            IncludeCategory = true,
            DateSegment = DateSegment.Year,
            SeqLength = 4,
            Separator = "-",
            ResetPeriod = ResetPeriod.Yearly,
            IsActive = true,
        };
        _db.NumberingRules.Add(rule);
        await _db.SaveChangesAsync();
        _ruleId = rule.Id;

        _svc = new NumberingService(_db, new NumberingClock());
    }

    public async Task DisposeAsync() => await _pg.DisposeAsync();

    private OneCupDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<OneCupDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .Options;
        return new OneCupDbContext(options);
    }

    [Fact]
    public async Task GenerateAsync_Serial_SequentialUnique()
    {
        // 串行取号 5 次，应得到 0001-0005
        var codes = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            using var txDb = NewDbContext();
            var svc = new NumberingService(txDb, new NumberingClock());
            string code;
            using (var tx = await txDb.Database.BeginTransactionAsync())
            {
                code = await svc.GenerateAsync("fabric", "COT");
                await tx.CommitAsync();
            }
            codes.Add(code);
        }
        Assert.Equal("FAB-COT-" + DateTime.UtcNow.AddHours(8).Year + "-0001", codes[0]);
        Assert.Equal(5, codes.Distinct().Count());
    }

    [Fact]
    public async Task GenerateAsync_Concurrent_AllUnique()
    {
        // 10 个并发任务，各取 10 次 = 100 个号，全部唯一
        const int tasks = 10, perTask = 10;
        var results = new List<string>[tasks];
        for (int i = 0; i < tasks; i++) results[i] = new List<string>();

        var tasksList = Enumerable.Range(0, tasks).Select(i => Task.Run(async () =>
        {
            for (int j = 0; j < perTask; j++)
            {
                using var txDb = NewDbContext();
                var svc = new NumberingService(txDb, new NumberingClock());
                string code;
                using (var tx = await txDb.Database.BeginTransactionAsync())
                {
                    code = await svc.GenerateAsync("fabric", "COT");
                    await tx.CommitAsync();
                }
                results[i].Add(code);
            }
        })).ToArray();
        await Task.WhenAll(tasksList);

        var all = results.SelectMany(x => x).ToList();
        Assert.Equal(tasks * perTask, all.Count);
        Assert.Equal(all.Count, all.Distinct().Count());  // 全部唯一
    }

    [Fact]
    public async Task GenerateAsync_NewCategory_StartsFromOne()
    {
        using var db = NewDbContext();
        var svc = new NumberingService(db, new NumberingClock());
        string code1;
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            code1 = await svc.GenerateAsync("fabric", "CHE");
            await tx.CommitAsync();
        }
        Assert.EndsWith("-0001", code1);
    }

    [Fact]
    public async Task GenerateAsync_DifferentCategories_Independent()
    {
        using var db = NewDbContext();
        var svc = new NumberingService(db, new NumberingClock());
        string cot1, che1, cot2;
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            cot1 = await svc.GenerateAsync("fabric", "COT");
            che1 = await svc.GenerateAsync("fabric", "CHE");
            cot2 = await svc.GenerateAsync("fabric", "COT");
            await tx.CommitAsync();
        }
        Assert.EndsWith("-0001", cot1);
        Assert.EndsWith("-0001", che1);
        Assert.EndsWith("-0002", cot2);  // COT 独立计数
    }

    [Fact]
    public async Task GenerateAsync_NoRule_Throws()
    {
        using var db = NewDbContext();
        var svc = new NumberingService(db, new NumberingClock());
        // fail-fast 守卫要求先开启事务（即便错误路径也会先检查 CurrentTransaction）
        using var tx = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<DomainException>(() => svc.GenerateAsync("nonexistent", null));
    }

    [Fact]
    public async Task GenerateAsync_CategoryRequired_Throws()
    {
        using var db = NewDbContext();
        var svc = new NumberingService(db, new NumberingClock());
        // fail-fast 守卫要求先开启事务
        using var tx = await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<DomainException>(() => svc.GenerateAsync("fabric", null));
    }

    [Fact]
    public async Task PreviewAsync_ReturnsNextWithoutConsuming()
    {
        using var db = NewDbContext();
        var svc = new NumberingService(db, new NumberingClock());
        var preview = await svc.PreviewAsync("fabric", "COT");
        Assert.NotNull(preview);
        // 预览不消耗计数：连续两次预览应相同
        var preview2 = await svc.PreviewAsync("fabric", "COT");
        Assert.Equal(preview, preview2);
    }

    [Fact]
    public async Task PreviewAsync_NoRule_ReturnsNull()
    {
        using var db = NewDbContext();
        var svc = new NumberingService(db, new NumberingClock());
        var preview = await svc.PreviewAsync("nonexistent");
        Assert.Null(preview);
    }
}
