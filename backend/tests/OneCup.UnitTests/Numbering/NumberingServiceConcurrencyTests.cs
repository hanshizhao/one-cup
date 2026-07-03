using Microsoft.EntityFrameworkCore;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Application.Services;
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

        // 字典分类数据（Task 6：引擎强校验依赖此字典）。
        // 注意：业务类型（fabric/material/equipment/…）已由 OneCupDbContext.Seed() 的 HasData 种入，
        // EnsureCreatedAsync 会应用该种子，因此这里【不再】Add fabric 类型——重复 Add 会违反
        // 唯一索引 ux_numbering_target_types_code。此处只补充分类（HasData 不种分类），供存量测试使用。
        _db.NumberingCategories.Add(new NumberingCategory
        {
            TargetTypeCode = "fabric", Code = "COT", NameZh = "棉", NameEn = "Cotton", IsActive = true,
        });
        _db.NumberingCategories.Add(new NumberingCategory
        {
            TargetTypeCode = "fabric", Code = "CHE", NameZh = "麻", NameEn = "Linen", IsActive = true,
        });

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

    // ──────────────────────────────────────────────────────────────
    // 字典强校验（Task 6 新增）
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_InvalidTargetType_Throws()
    {
        // 字典里没有 "ghost" 业务类型 → 应拒绝
        using var txDb = NewDbContext();
        var svc = new NumberingService(txDb, new NumberingClock());
        using var tx = await txDb.Database.BeginTransactionAsync();
        var ex = await Assert.ThrowsAsync<DomainException>(() => svc.GenerateAsync("ghost", null));
        Assert.Contains("ghost", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_DisabledTargetType_Throws()
    {
        // 种入停用的业务类型 → 调用应拒绝
        using var seedDb = NewDbContext();
        seedDb.NumberingTargetTypes.Add(new NumberingTargetType
        {
            Code = "disabled", NameZh = "停用类型", NameEn = "Disabled", IsActive = false,
        });
        seedDb.NumberingRules.Add(new NumberingRule
        {
            TargetType = "disabled", Name = "停用规则", Prefix = "DIS",
            IncludeCategory = false, DateSegment = DateSegment.None,
            SeqLength = 4, Separator = "-", ResetPeriod = ResetPeriod.None, IsActive = true,
        });
        await seedDb.SaveChangesAsync();

        using var txDb = NewDbContext();
        var svc = new NumberingService(txDb, new NumberingClock());
        using var tx = await txDb.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<DomainException>(() => svc.GenerateAsync("disabled", null));
    }

    [Fact]
    public async Task GenerateAsync_ValidTypeNoCategory_WithCategoryRule_Throws()
    {
        // 用 material 业务类型（HasData 已种入，无分类字典，且 InitializeAsync 未给其建规则）。
        // 规则要求分类码，但传字典里不存在的分类码 → 应拒绝。
        // 不用 fabric：InitializeAsync 已建一条 fabric 启用规则，再 Add 会违反部分唯一索引
        // ux_numbering_rules_target_type_active（同类型仅一条启用规则）。
        using var seedDb = NewDbContext();
        seedDb.NumberingRules.Add(new NumberingRule
        {
            TargetType = "material", Name = "原料规则", Prefix = "MAT",
            IncludeCategory = true, DateSegment = DateSegment.None,
            SeqLength = 4, Separator = "-", ResetPeriod = ResetPeriod.None, IsActive = true,
        });
        await seedDb.SaveChangesAsync();

        using var txDb = NewDbContext();
        var svc = new NumberingService(txDb, new NumberingClock());
        using var tx = await txDb.Database.BeginTransactionAsync();
        // material 类型存在且启用，规则要求分类，但 "GHOST" 不在字典 → 应拒绝
        await Assert.ThrowsAsync<DomainException>(() => svc.GenerateAsync("material", "GHOST"));
    }

    [Fact]
    public async Task GenerateAsync_ValidTypeValidCategory_Succeeds()
    {
        // 字典已有 fabric + COT（InitializeAsync 种入）→ 正常取号
        using var txDb = NewDbContext();
        var svc = new NumberingService(txDb, new NumberingClock());
        string code;
        using (var tx = await txDb.Database.BeginTransactionAsync())
        {
            code = await svc.GenerateAsync("fabric", "COT");
            await tx.CommitAsync();
        }
        Assert.Contains("COT", code);
    }

    [Fact]
    public async Task GenerateAsync_DisabledCategory_Throws()
    {
        // 种入 fabric 下停用的分类码 → 调用应拒绝
        using var seedDb = NewDbContext();
        seedDb.NumberingCategories.Add(new NumberingCategory
        {
            TargetTypeCode = "fabric", Code = "WOOL", NameZh = "羊毛", NameEn = "Wool", IsActive = false,
        });
        await seedDb.SaveChangesAsync();

        using var txDb = NewDbContext();
        var svc = new NumberingService(txDb, new NumberingClock());
        using var tx = await txDb.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<DomainException>(() => svc.GenerateAsync("fabric", "WOOL"));
    }

    [Fact]
    public async Task GenerateAsync_NoCategoryRule_IgnoresPassedCategory()
    {
        // 规则 IncludeCategory=false → 即使传了字典里没有的分类码也不校验（宽容忽略保持不变）。
        // 用 equipment 业务类型（HasData 已种入，InitializeAsync 未给其建规则），避免与 fabric 启用规则
        // 冲突部分唯一索引 ux_numbering_rules_target_type_active。
        using var seedDb = NewDbContext();
        seedDb.NumberingRules.Add(new NumberingRule
        {
            TargetType = "equipment", Name = "设备规则-无分类", Prefix = "EQP",
            IncludeCategory = false, DateSegment = DateSegment.None,
            SeqLength = 4, Separator = "-", ResetPeriod = ResetPeriod.None, IsActive = true,
        });
        await seedDb.SaveChangesAsync();

        using var txDb = NewDbContext();
        var svc = new NumberingService(txDb, new NumberingClock());
        string code;
        using (var tx = await txDb.Database.BeginTransactionAsync())
        {
            code = await svc.GenerateAsync("equipment", "ANYTHING");
            await tx.CommitAsync();
        }
        Assert.StartsWith("EQP", code);
        Assert.DoesNotContain("ANYTHING", code);
    }
}
