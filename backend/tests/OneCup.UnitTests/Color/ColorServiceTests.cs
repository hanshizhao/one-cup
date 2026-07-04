using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Application.Interfaces;
using OneCup.Application.Services;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Color;

public class ColorServiceTests
{
    private static (OneCupDbContext db, ColorService svc, FakeNumberingService numbering) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"color-{Guid.NewGuid()}")
            // CreateColorAsync 现在用 ExecuteInTransactionAsync，InMemory 忽略事务会告警
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        var numbering = new FakeNumberingService();
        var svc = new ColorService(new Repository<Domain.Entities.Color>(db), new UnitOfWork(db), numbering);
        return (db, svc, numbering);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static CreateColorRequest ValidCreate() => new()
    {
        NameZh = "大红", NameEn = "Red",
        Hex = "#FF0000", ColorFamily = "红", SortOrder = 1,
    };

    // ── 新增 ──

    [Fact]
    public async Task CreateColorAsync_CreatesColor_WithGeneratedCode()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "COL-0001";

        var dto = await svc.CreateColorAsync(ValidCreate());
        Assert.Equal("COL-0001", dto.Code);   // code 由编号引擎生成
        Assert.Equal("#FF0000", dto.Hex);
        Assert.True(dto.IsActive);
    }

    [Theory]
    [MemberData(nameof(InvalidHexCases))]
    public async Task CreateColorAsync_InvalidHex_Throws(string hex)
    {
        var (_, svc, _) = Setup();
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
        var (_, svc, _) = Setup();
        var req = ValidCreate() with { Hex = hex };
        var dto = await svc.CreateColorAsync(req);
        Assert.Equal(hex, dto.Hex);
    }

    // ── 编辑 ──

    [Fact]
    public async Task UpdateColorAsync_CodeImmutable_FieldsUpdatable()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "COL-0001";
        var dto = await svc.CreateColorAsync(ValidCreate());
        await svc.UpdateColorAsync(dto.Id, new UpdateColorRequest
        {
            NameZh = "大红改", Hex = "#EE0000", ColorFamily = "深红",
            Remark = "备注", SortOrder = 5,
        });
        var updated = await svc.GetColorAsync(dto.Id);
        Assert.Equal("COL-0001", updated!.Code);         // code 由引擎生成，编辑不可改
        Assert.Equal("大红改", updated.NameZh);
        Assert.Equal("#EE0000", updated.Hex);
        Assert.Equal("深红", updated.ColorFamily);
        Assert.Equal("备注", updated.Remark);
        Assert.Equal(5, updated.SortOrder);
    }

    [Fact]
    public async Task UpdateColorAsync_InvalidHex_Throws()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.CreateColorAsync(ValidCreate());
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateColorAsync(dto.Id, new UpdateColorRequest { Hex = "nothex" }));
    }

    [Fact]
    public async Task UpdateColorAsync_NotFound_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateColorAsync(Guid.NewGuid(), new UpdateColorRequest { NameZh = "x" }));
    }

    // ── 启停 ──

    [Fact]
    public async Task UpdateColorStatusAsync_Toggles()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.CreateColorAsync(ValidCreate());
        await svc.UpdateColorStatusAsync(dto.Id, false);
        var updated = await svc.GetColorAsync(dto.Id);
        Assert.False(updated!.IsActive);
    }

    // ── 查询 ──

    [Fact]
    public async Task GetColorsAsync_FiltersByKeyword()
    {
        var (_, svc, _) = Setup();
        await svc.CreateColorAsync(ValidCreate() with { NameZh = "大红" });
        await svc.CreateColorAsync(ValidCreate() with { NameZh = "海蓝" });
        var res = await svc.GetColorsAsync(1, 10, "大红", null, null);
        Assert.Single(res.Items);
    }

    [Fact]
    public async Task GetColorsAsync_FiltersByColorFamily()
    {
        var (_, svc, _) = Setup();
        await svc.CreateColorAsync(ValidCreate() with { ColorFamily = "红" });
        await svc.CreateColorAsync(ValidCreate() with { ColorFamily = "红" });
        await svc.CreateColorAsync(ValidCreate() with { ColorFamily = "蓝" });
        var res = await svc.GetColorsAsync(1, 10, null, "红", null);
        Assert.Equal(2, res.Total);
    }

    [Fact]
    public async Task GetColorsAsync_TotalUnaffectedByPaging()
    {
        var (_, svc, _) = Setup();
        for (var i = 0; i < 5; i++)
            await svc.CreateColorAsync(ValidCreate() with { ColorFamily = "红" });
        var res = await svc.GetColorsAsync(1, 2, null, "红", null);
        Assert.Equal(2, res.Items.Count);
        Assert.Equal(5, res.Total);   // total 不受分页污染
    }

    [Fact]
    public async Task GetAllActiveColorsAsync_ReturnsOnlyActiveOrdered()
    {
        var (_, svc, _) = Setup();
        var a = await svc.CreateColorAsync(ValidCreate() with { SortOrder = 2 });
        var b = await svc.CreateColorAsync(ValidCreate() with { SortOrder = 1 });
        await svc.UpdateColorStatusAsync(a.Id, false);
        var list = await svc.GetAllActiveColorsAsync();
        Assert.Single(list);
        Assert.Equal(b.Id, list[0].Id);   // 只有 b 启用
    }

    [Fact]
    public async Task GetColorAsync_NotFound_ReturnsNull()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.GetColorAsync(Guid.NewGuid());
        Assert.Null(dto);
    }
}

/// <summary>
/// 编号引擎的测试替身：每次 GenerateAsync 返回自增 code（COL-0001, COL-0002 ...），
/// 模拟真实引擎的"每次取号唯一"行为。NextCode 可显式覆盖首次返回值。
/// </summary>
internal sealed class FakeNumberingService : INumberingService
{
    private int _seq;
    public string? NextCode { get; set; }

    public Task<string> GenerateAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
    {
        if (NextCode is not null)
        {
            var code = NextCode;
            NextCode = null;   // 仅首次用显式值，之后自增
            return Task.FromResult(code);
        }
        _seq++;
        return Task.FromResult($"COL-{_seq:D4}");
    }

    public Task<string?> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
        => Task.FromResult<string?>(NextCode ?? $"COL-{(_seq + 1):D4}");
}
