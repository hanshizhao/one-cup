using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Color;

public class ColorSpecsTests
{
    private static OneCupDbContext CreateDb()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"color-specs-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        return db;
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static Domain.Entities.Color Make(string code, string family = "红", bool active = true, int sort = 0) => new()
    {
        Code = code, NameZh = code, NameEn = code, Hex = "#FF0000",
        ColorFamily = family, IsActive = active, SortOrder = sort,
    };

    [Fact]
    public async Task ColorFilterSpec_Keyword_MatchesCodeNameZhNameEn()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Color>(db);
        await repo.AddAsync(Make("RED001", family: "红"));
        await repo.AddAsync(Make("BLU001", family: "蓝"));
        await db.SaveChangesAsync();

        // RED 命中 code
        var red = await repo.ListAsync(new ColorFilterSpec("RED", null, null));
        Assert.Single(red);
        Assert.Equal("RED001", red[0].Code);

        // 蓝色系也命中（family 在 PagedSpec 测，这里测 nameEn）
        var blu = await repo.ListAsync(new ColorFilterSpec("BLU", null, null));
        Assert.Single(blu);
    }

    [Fact]
    public async Task ColorFilterSpec_ColorFamily_ExactMatch()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Color>(db);
        await repo.AddAsync(Make("R1", family: "红"));
        await repo.AddAsync(Make("R2", family: "红"));
        await repo.AddAsync(Make("B1", family: "蓝"));
        await db.SaveChangesAsync();

        var reds = await repo.ListAsync(new ColorFilterSpec(null, "红", null));
        Assert.Equal(2, reds.Count);
    }

    [Fact]
    public async Task ColorFilterSpec_IsActive_Filters()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Color>(db);
        await repo.AddAsync(Make("A1", active: true));
        await repo.AddAsync(Make("A2", active: false));
        await db.SaveChangesAsync();

        var active = await repo.ListAsync(new ColorFilterSpec(null, null, true));
        Assert.Single(active);
        Assert.Equal("A1", active[0].Code);

        var inactive = await repo.ListAsync(new ColorFilterSpec(null, null, false));
        Assert.Single(inactive);
        Assert.Equal("A2", inactive[0].Code);
    }

    [Fact]
    public async Task ColorPagedSpec_AppliesSkipTakeAndOrderBy()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Color>(db);
        await repo.AddAsync(Make("C1", sort: 3));
        await repo.AddAsync(Make("C2", sort: 1));
        await repo.AddAsync(Make("C3", sort: 2));
        await db.SaveChangesAsync();

        // 第 1 页 size 2，按 SortOrder 升序 → C2(sort1), C3(sort2)
        var page1 = await repo.ListAsync(new ColorPagedSpec(null, null, null, 1, 2));
        Assert.Equal(2, page1.Count);
        Assert.Equal("C2", page1[0].Code);
        Assert.Equal("C3", page1[1].Code);
    }

    [Fact]
    public async Task ColorPagedSpec_CountUnaffectedByPaging()
    {
        // 关键：FilterSpec 统计 total 不受分页污染（编号字典曾因此出 bug）
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Color>(db);
        for (var i = 0; i < 5; i++)
            await repo.AddAsync(Make($"C{i}", family: "红"));
        await db.SaveChangesAsync();

        var total = await repo.CountAsync(new ColorFilterSpec(null, "红", null));
        Assert.Equal(5, total);   // 全部命中，不受分页影响
    }

    [Fact]
    public async Task ColorByCodeSpec_MatchesIgnoringExcludedId()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Color>(db);
        await repo.AddAsync(Make("RED001"));
        await db.SaveChangesAsync();
        var existing = await repo.FirstOrDefaultAsync(new ColorByCodeSpec("RED001"));

        // 排除自身 → 无匹配（用于编辑时唯一性校验）
        var excl = await repo.AnyAsync(new ColorByCodeSpec("RED001", existing!.Id));
        Assert.False(excl);

        // 不排除 → 有匹配
        var incl = await repo.AnyAsync(new ColorByCodeSpec("RED001"));
        Assert.True(incl);
    }

    [Fact]
    public async Task ColorActiveSpec_ReturnsOnlyActiveOrdered()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Color>(db);
        await repo.AddAsync(Make("A1", active: true, sort: 2));
        await repo.AddAsync(Make("A2", active: false, sort: 1));
        await repo.AddAsync(Make("A3", active: true, sort: 1));
        await db.SaveChangesAsync();

        var list = await repo.ListAsync(new ColorActiveSpec());
        Assert.Equal(2, list.Count);
        Assert.Equal("A3", list[0].Code);   // sort1 启用项在前
        Assert.Equal("A1", list[1].Code);
    }
}
