using Microsoft.EntityFrameworkCore;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using OneCup.Infrastructure.Services;
using Testcontainers.PostgreSql;

namespace OneCup.UnitTests.Numbering;

/// <summary>
/// 并发取号测试。必须用真实 PostgreSQL——InMemory 不支持 FOR UPDATE 行锁，会给虚假安全感。
///
/// 双模式连接：
/// 1. 若设了环境变量 NUMBERING_TEST_PG，用它作为连接串（连已运行的 PG，如云上开发库）。
///    强烈建议指向独立测试库（如 onecup_numbering_test），避免污染开发数据。
///    每个测试类实例会先 DROP+CREATE 该库的表（EnsureDeleted + EnsureCreated），保证干净。
/// 2. 否则回退 Testcontainers（需要本机 Docker）。
/// </summary>
public class NumberingServiceConcurrencyTests : IAsyncLifetime
{
    private OneCupDbContext _db = null!;
    private NumberingService _svc = null!;
    private Guid _ruleId;
    private string _connectionString = null!;
    private PostgreSqlContainer? _pg;

    public async Task InitializeAsync()
    {
        var envConn = Environment.GetEnvironmentVariable("NUMBERING_TEST_PG");
        // 也支持从文件读连接串（避开 shell 对含特殊字符密码的转义问题）
        var connFile = Environment.GetEnvironmentVariable("NUMBERING_TEST_PG_FILE");
        if (!string.IsNullOrEmpty(connFile) && File.Exists(connFile))
            envConn = File.ReadAllText(connFile).Trim();
        if (!string.IsNullOrEmpty(envConn))
        {
            // 模式1：连已运行的 PG（云上/本地）。先 DROP 再 CREATE schema，保证每个测试类干净。
            _connectionString = envConn;
        }
        else
        {
            // 模式2：Testcontainers 起独立容器（需 Docker）
            _pg = new PostgreSqlBuilder()
                .WithImage("postgres:17-alpine")
                .WithDatabase("numbering_test")
                .WithUsername("test")
                .WithPassword("test")
                .Build();
            await _pg.StartAsync();
            _connectionString = _pg.GetConnectionString();
        }

        var options = new DbContextOptionsBuilder<OneCupDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        _db = new OneCupDbContext(options);

        // 模式1下：重建 schema（DROP 所有表再建）。Testcontainers 是全新容器无需此步，但执行也无害。
        await _db.Database.EnsureDeletedAsync();
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

    public async Task DisposeAsync()
    {
        // 模式1下可选清理（保留库，留 schema 供下次重建）。Testcontainers 容器释放。
        if (_pg is not null) await _pg.DisposeAsync();
    }

    private OneCupDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<OneCupDbContext>()
            .UseNpgsql(_connectionString)
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
