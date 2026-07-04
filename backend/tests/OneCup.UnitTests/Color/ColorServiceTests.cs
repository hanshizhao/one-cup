using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Application.Services;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Color;

public class ColorServiceTests
{
    private static (OneCupDbContext db, ColorService svc) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"color-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        var svc = new ColorService(new Repository<Domain.Entities.Color>(db), new UnitOfWork(db));
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static CreateColorRequest ValidCreate(string code = "RED001") => new()
    {
        Code = code, NameZh = "大红", NameEn = "Red",
        Hex = "#FF0000", ColorFamily = "红", SortOrder = 1,
    };

    // ── 新增 ──

    [Fact]
    public async Task CreateColorAsync_CreatesColor()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateColorAsync(ValidCreate());
        Assert.Equal("RED001", dto.Code);
        Assert.Equal("#FF0000", dto.Hex);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task CreateColorAsync_DuplicateCode_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateColorAsync(ValidCreate("RED001"));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateColorAsync(ValidCreate("RED001")));
    }

    [Fact]
    public async Task CreateColorAsync_DuplicateCodeEvenWhenInactive_Throws()
    {
        // 停用的 code 仍占用（唯一性校验不含 IsActive 过滤）
        var (db, svc) = Setup();
        var dto = await svc.CreateColorAsync(ValidCreate("RED001"));
        await svc.UpdateColorStatusAsync(dto.Id, false);
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateColorAsync(ValidCreate("RED001")));
    }

    [Theory]
    [MemberData(nameof(InvalidHexCases))]
    public async Task CreateColorAsync_InvalidHex_Throws(string hex)
    {
        var (db, svc) = Setup();
        var req = ValidCreate() with { Hex = hex };
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateColorAsync(req));
    }

    public static IEnumerable<object[]> InvalidHexCases => new[]
    {
        new object[] { "FF0000" },    // 缺 #
        new object[] { "#FF00" },     // 长度不足
        new object[] { "#GGGGGG" },   // 非法字符
        new object[] { "#ff00001" },  // 长度超
    };

    [Theory]
    [InlineData("#FF0000")]
    [InlineData("#ff0000")]
    [InlineData("#AbCdEf")]
    public async Task CreateColorAsync_ValidHex_Accepted(string hex)
    {
        var (db, svc) = Setup();
        var req = ValidCreate() with { Hex = hex };
        var dto = await svc.CreateColorAsync(req);
        Assert.Equal(hex, dto.Hex);
    }

    // ── 编辑 ──

    [Fact]
    public async Task UpdateColorAsync_CodeIgnored_FieldsUpdatable()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateColorAsync(ValidCreate());
        await svc.UpdateColorAsync(dto.Id, new UpdateColorRequest
        {
            NameZh = "大红改", Hex = "#EE0000", ColorFamily = "深红",
            Remark = "备注", SortOrder = 5,
        });
        var updated = await svc.GetColorAsync(dto.Id);
        Assert.Equal("RED001", updated!.Code);          // code 不变（接口不暴露 Code）
        Assert.Equal("大红改", updated.NameZh);
        Assert.Equal("#EE0000", updated.Hex);
        Assert.Equal("深红", updated.ColorFamily);
        Assert.Equal("备注", updated.Remark);
        Assert.Equal(5, updated.SortOrder);
    }

    [Fact]
    public async Task UpdateColorAsync_InvalidHex_Throws()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateColorAsync(ValidCreate());
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateColorAsync(dto.Id, new UpdateColorRequest { Hex = "nothex" }));
    }

    [Fact]
    public async Task UpdateColorAsync_NotFound_Throws()
    {
        var (db, svc) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateColorAsync(Guid.NewGuid(), new UpdateColorRequest { NameZh = "x" }));
    }

    // ── 启停 ──

    [Fact]
    public async Task UpdateColorStatusAsync_Toggles()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateColorAsync(ValidCreate());
        await svc.UpdateColorStatusAsync(dto.Id, false);
        var updated = await svc.GetColorAsync(dto.Id);
        Assert.False(updated!.IsActive);
    }

    // ── 查询 ──

    [Fact]
    public async Task GetColorsAsync_FiltersByKeyword()
    {
        var (db, svc) = Setup();
        await svc.CreateColorAsync(ValidCreate("RED001") with { NameZh = "大红" });
        await svc.CreateColorAsync(ValidCreate("BLU001") with { NameZh = "海蓝" });
        var res = await svc.GetColorsAsync(1, 10, "RED", null, null);
        Assert.Single(res.Items);
    }

    [Fact]
    public async Task GetColorsAsync_FiltersByColorFamily()
    {
        var (db, svc) = Setup();
        await svc.CreateColorAsync(ValidCreate("R1") with { ColorFamily = "红" });
        await svc.CreateColorAsync(ValidCreate("R2") with { ColorFamily = "红" });
        await svc.CreateColorAsync(ValidCreate("B1") with { ColorFamily = "蓝" });
        var res = await svc.GetColorsAsync(1, 10, null, "红", null);
        Assert.Equal(2, res.Total);
    }

    [Fact]
    public async Task GetColorsAsync_TotalUnaffectedByPaging()
    {
        var (db, svc) = Setup();
        for (var i = 0; i < 5; i++)
            await svc.CreateColorAsync(ValidCreate($"C{i}") with { ColorFamily = "红" });
        var res = await svc.GetColorsAsync(1, 2, null, "红", null);
        Assert.Equal(2, res.Items.Count);
        Assert.Equal(5, res.Total);   // total 不受分页污染
    }

    [Fact]
    public async Task GetAllActiveColorsAsync_ReturnsOnlyActiveOrdered()
    {
        var (db, svc) = Setup();
        var a = await svc.CreateColorAsync(ValidCreate("A1") with { SortOrder = 2 });
        var b = await svc.CreateColorAsync(ValidCreate("A2") with { SortOrder = 1 });
        await svc.UpdateColorStatusAsync(a.Id, false);
        var list = await svc.GetAllActiveColorsAsync();
        Assert.Single(list);
        Assert.Equal("A2", list[0].Code);
    }

    [Fact]
    public async Task GetColorAsync_NotFound_ReturnsNull()
    {
        var (db, svc) = Setup();
        var dto = await svc.GetColorAsync(Guid.NewGuid());
        Assert.Null(dto);
    }
}
