using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Application.Interfaces;
using OneCup.Application.Services;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Material;

public class MaterialServiceTests
{
    private static (OneCupDbContext db, MaterialService svc, FakeNumberingService numbering) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"material-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        var numbering = new FakeNumberingService();
        var svc = new MaterialService(
            new Repository<Domain.Entities.Material>(db),
            new UnitOfWork(db),
            numbering,
            new CreateMaterialRequestValidator(),
            new UpdateMaterialRequestValidator());
        return (db, svc, numbering);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static CreateMaterialRequest ValidCreate() => new()
    {
        Name = "活性红 3B", Spec = "粉末 100%", Category = "染料", SortOrder = 1,
    };

    // ── 新增 ──

    [Fact]
    public async Task CreateMaterialAsync_CreatesMaterial_WithGeneratedCode()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "MAT-0001";

        var dto = await svc.CreateMaterialAsync(ValidCreate());
        Assert.Equal("MAT-0001", dto.Code);   // code 由编号引擎生成
        Assert.Equal("活性红 3B", dto.Name);
        Assert.Equal("粉末 100%", dto.Spec);
        Assert.Equal("染料", dto.Category);
        Assert.True(dto.IsActive);
        Assert.Null(dto.UnitId);   // 默认无单位
    }

    [Fact]
    public async Task CreateMaterialAsync_WithUnitId_StoresUnitId()
    {
        var (_, svc, _) = Setup();
        var unitId = Guid.NewGuid();
        var dto = await svc.CreateMaterialAsync(ValidCreate() with { UnitId = unitId });
        Assert.Equal(unitId, dto.UnitId);
    }

    [Fact]
    public async Task CreateMaterialAsync_EmptyName_ThrowsValidation()
    {
        // FluentValidation 拦截:Name 必填,空串/空白校验失败。
        // EnsureValidAsync 扩展把校验失败包装成 DomainException(项目约定,→400)
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateMaterialAsync(ValidCreate() with { Name = "" }));
    }

    [Fact]
    public async Task CreateMaterialAsync_OverlongSpec_ThrowsValidation()
    {
        var (_, svc, _) = Setup();
        var tooLong = new string('x', 101);   // Spec 上限 100
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateMaterialAsync(ValidCreate() with { Spec = tooLong }));
    }

    // ── 编辑 ──

    [Fact]
    public async Task UpdateMaterialAsync_CodeImmutable_FieldsUpdatable()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "MAT-0001";
        var dto = await svc.CreateMaterialAsync(ValidCreate());

        var unitId = Guid.NewGuid();
        await svc.UpdateMaterialAsync(dto.Id, new UpdateMaterialRequest
        {
            Name = "活性红改", Spec = "液体 50%", Category = "助剂",
            UnitId = unitId, Remark = "备注", SortOrder = 5,
        });

        var updated = await svc.GetMaterialAsync(dto.Id);
        Assert.Equal("MAT-0001", updated!.Code);   // code 不可改
        Assert.Equal("活性红改", updated.Name);
        Assert.Equal("液体 50%", updated.Spec);
        Assert.Equal("助剂", updated.Category);
        Assert.Equal(unitId, updated.UnitId);
        Assert.Equal("备注", updated.Remark);
        Assert.Equal(5, updated.SortOrder);
    }

    [Fact]
    public async Task UpdateMaterialAsync_NotFound_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateMaterialAsync(Guid.NewGuid(), new UpdateMaterialRequest { Name = "x" }));
    }

    [Fact]
    public async Task UpdateMaterialAsync_NullUnitId_ClearsUnit()
    {
        // 关键:整表覆盖式 PUT(对齐 Customer),不做 null-skip。
        // 否则用户清空单位下拉提交 null 会被当"不修改",单位无法清空。
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "MAT-0001";
        var unitId = Guid.NewGuid();
        var dto = await svc.CreateMaterialAsync(ValidCreate() with { UnitId = unitId });
        Assert.Equal(unitId, dto.UnitId);

        await svc.UpdateMaterialAsync(dto.Id, new UpdateMaterialRequest { UnitId = null });
        var updated = await svc.GetMaterialAsync(dto.Id);
        Assert.Null(updated!.UnitId);   // 单位被清空,而非保留原值
    }

    // ── 启停 ──

    [Fact]
    public async Task UpdateMaterialStatusAsync_Toggles()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.CreateMaterialAsync(ValidCreate());
        await svc.UpdateMaterialStatusAsync(dto.Id, false);
        var updated = await svc.GetMaterialAsync(dto.Id);
        Assert.False(updated!.IsActive);
    }

    // ── 删除 ──

    [Fact]
    public async Task DeleteMaterialAsync_RemovesEntity()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.CreateMaterialAsync(ValidCreate());
        await svc.DeleteMaterialAsync(dto.Id);
        var after = await svc.GetMaterialAsync(dto.Id);
        Assert.Null(after);   // 物理删除
    }

    [Fact]
    public async Task DeleteMaterialAsync_NotFound_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.DeleteMaterialAsync(Guid.NewGuid()));
    }

    // ── 查询 ──

    [Fact]
    public async Task GetMaterialsAsync_FiltersByKeyword()
    {
        var (_, svc, _) = Setup();
        await svc.CreateMaterialAsync(ValidCreate() with { Name = "活性红" });
        await svc.CreateMaterialAsync(ValidCreate() with { Name = "渗透剂" });
        var res = await svc.GetMaterialsAsync(1, 10, "活性", null, null);
        Assert.Single(res.Items);
    }

    [Fact]
    public async Task GetMaterialsAsync_FiltersByKeyword_MatchesSpec()
    {
        // keyword 也搜 spec 字段
        var (_, svc, _) = Setup();
        await svc.CreateMaterialAsync(ValidCreate() with { Name = "某染料", Spec = "粉末 100%" });
        await svc.CreateMaterialAsync(ValidCreate() with { Name = "其他", Spec = "液体 50%" });
        var res = await svc.GetMaterialsAsync(1, 10, "粉末", null, null);
        Assert.Single(res.Items);
    }

    [Fact]
    public async Task GetMaterialsAsync_FiltersByCategory()
    {
        var (_, svc, _) = Setup();
        await svc.CreateMaterialAsync(ValidCreate() with { Category = "染料" });
        await svc.CreateMaterialAsync(ValidCreate() with { Category = "染料" });
        await svc.CreateMaterialAsync(ValidCreate() with { Category = "助剂" });
        var res = await svc.GetMaterialsAsync(1, 10, null, "染料", null);
        Assert.Equal(2, res.Total);
    }

    [Fact]
    public async Task GetMaterialsAsync_TotalUnaffectedByPaging()
    {
        var (_, svc, _) = Setup();
        for (var i = 0; i < 5; i++)
            await svc.CreateMaterialAsync(ValidCreate() with { Category = "染料" });
        var res = await svc.GetMaterialsAsync(1, 2, null, "染料", null);
        Assert.Equal(2, res.Items.Count);
        Assert.Equal(5, res.Total);   // total 不受分页污染
    }

    [Fact]
    public async Task GetAllActiveMaterialsAsync_ReturnsOnlyActiveOrdered()
    {
        var (_, svc, _) = Setup();
        var a = await svc.CreateMaterialAsync(ValidCreate() with { SortOrder = 2 });
        var b = await svc.CreateMaterialAsync(ValidCreate() with { SortOrder = 1 });
        await svc.UpdateMaterialStatusAsync(a.Id, false);
        var list = await svc.GetAllActiveMaterialsAsync();
        Assert.Single(list);
        Assert.Equal(b.Id, list[0].Id);
    }

    [Fact]
    public async Task GetMaterialAsync_NotFound_ReturnsNull()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.GetMaterialAsync(Guid.NewGuid());
        Assert.Null(dto);
    }
}

/// <summary>
/// 编号引擎测试替身:每次 GenerateAsync 返回自增 code(MAT-0001, MAT-0002 ...)。
/// NextCode 可显式覆盖首次返回值。照搬 Color 测试的 FakeNumberingService,改前缀为 MAT-。
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
            NextCode = null;
            return Task.FromResult(code);
        }
        _seq++;
        return Task.FromResult($"MAT-{_seq:D4}");
    }

    public Task<PreviewResult> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
        => Task.FromResult(new PreviewResult { Code = NextCode ?? $"MAT-{(_seq + 1):D4}", IncludeCategory = false });
}
