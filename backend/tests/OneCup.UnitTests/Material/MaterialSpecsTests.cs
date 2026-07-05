using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Material;

public class MaterialSpecsTests
{
    private static OneCupDbContext CreateDb()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"material-specs-{Guid.NewGuid()}")
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

    private static Domain.Entities.Material Make(string code, string category = "染料", bool active = true, int sort = 0) => new()
    {
        Code = code, Name = code, Spec = code, Category = category,
        IsActive = active, SortOrder = sort,
    };

    [Fact]
    public async Task MaterialFilterSpec_Keyword_MatchesCodeNameSpec()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        await repo.AddAsync(Make("MAT001"));
        await repo.AddAsync(Make("AUX001"));
        await db.SaveChangesAsync();

        var mat = await repo.ListAsync(new MaterialFilterSpec("MAT", null, null));
        Assert.Single(mat);
        Assert.Equal("MAT001", mat[0].Code);

        var aux = await repo.ListAsync(new MaterialFilterSpec("AUX", null, null));
        Assert.Single(aux);
    }

    [Fact]
    public async Task MaterialFilterSpec_Category_ExactMatch()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        await repo.AddAsync(Make("M1", category: "染料"));
        await repo.AddAsync(Make("M2", category: "染料"));
        await repo.AddAsync(Make("A1", category: "助剂"));
        await db.SaveChangesAsync();

        var dyes = await repo.ListAsync(new MaterialFilterSpec(null, "染料", null));
        Assert.Equal(2, dyes.Count);
    }

    [Fact]
    public async Task MaterialFilterSpec_IsActive_Filters()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        await repo.AddAsync(Make("A1", active: true));
        await repo.AddAsync(Make("A2", active: false));
        await db.SaveChangesAsync();

        var active = await repo.ListAsync(new MaterialFilterSpec(null, null, true));
        Assert.Single(active);
        Assert.Equal("A1", active[0].Code);

        var inactive = await repo.ListAsync(new MaterialFilterSpec(null, null, false));
        Assert.Single(inactive);
        Assert.Equal("A2", inactive[0].Code);
    }

    [Fact]
    public async Task MaterialPagedSpec_AppliesSkipTakeAndOrderBy()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        await repo.AddAsync(Make("M1", sort: 3));
        await repo.AddAsync(Make("M2", sort: 1));
        await repo.AddAsync(Make("M3", sort: 2));
        await db.SaveChangesAsync();

        var page1 = await repo.ListAsync(new MaterialPagedSpec(null, null, null, 1, 2));
        Assert.Equal(2, page1.Count);
        Assert.Equal("M2", page1[0].Code);   // sort1
        Assert.Equal("M3", page1[1].Code);   // sort2
    }

    [Fact]
    public async Task MaterialFilterSpec_CountUnaffectedByPaging()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        for (var i = 0; i < 5; i++)
            await repo.AddAsync(Make($"M{i}", category: "染料"));
        await db.SaveChangesAsync();

        var total = await repo.CountAsync(new MaterialFilterSpec(null, "染料", null));
        Assert.Equal(5, total);
    }

    [Fact]
    public async Task MaterialByCodeSpec_MatchesIgnoringExcludedId()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        await repo.AddAsync(Make("MAT001"));
        await db.SaveChangesAsync();
        var existing = await repo.FirstOrDefaultAsync(new MaterialByCodeSpec("MAT001"));

        var excl = await repo.AnyAsync(new MaterialByCodeSpec("MAT001", existing!.Id));
        Assert.False(excl);

        var incl = await repo.AnyAsync(new MaterialByCodeSpec("MAT001"));
        Assert.True(incl);
    }

    [Fact]
    public async Task MaterialActiveSpec_ReturnsOnlyActiveOrdered()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        await repo.AddAsync(Make("A1", active: true, sort: 2));
        await repo.AddAsync(Make("A2", active: false, sort: 1));
        await repo.AddAsync(Make("A3", active: true, sort: 1));
        await db.SaveChangesAsync();

        var list = await repo.ListAsync(new MaterialActiveSpec());
        Assert.Equal(2, list.Count);
        Assert.Equal("A3", list[0].Code);   // sort1 启用项在前
        Assert.Equal("A1", list[1].Code);
    }
}
