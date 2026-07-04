using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Services;
using OneCup.Application.Validators.System;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
// 命名空间 OneCup.UnitTests.Process 内，未限定的 "Process" 可能被解析为
// System.Diagnostics.Process；用别名显式指向实体类型。
using ProcessEntity = OneCup.Domain.Entities.Process;

namespace OneCup.UnitTests.Process;

public class ProcessServiceTests
{
    private static (OneCupDbContext db, ProcessService svc, FakeNumberingService numbering) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"process-test-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);

        var numbering = new FakeNumberingService();
        var svc = new ProcessService(
            new Repository<ProcessEntity>(db),
            new UnitOfWork(db),
            numbering,
            new CreateProcessRequestValidator(),
            new UpdateProcessRequestValidator());
        return (db, svc, numbering);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static CreateProcessRequest ValidCreate() => new()
    {
        Name = "染色",
        Category = "前处理",
        SortOrder = 1,
    };

    // ── 新增 ──

    [Fact]
    public async Task CreateAsync_CreatesProcess_WithGeneratedCode()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "PRC-0001";

        var dto = await svc.CreateAsync(ValidCreate());
        Assert.Equal("PRC-0001", dto.Code);   // code 由编号引擎生成
        Assert.Equal("染色", dto.Name);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_InSameCategory_Throws()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate());   // 染色/前处理
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(ValidCreate() with { SortOrder = 2 }));  // 同名同分类
    }

    [Fact]
    public async Task CreateAsync_SameName_DifferentCategory_Allowed()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate() with { Category = "前处理" });   // 染色/前处理
        // 跨分类同名应允许
        var dto = await svc.CreateAsync(ValidCreate() with { Category = "后整理", SortOrder = 2 });
        Assert.Equal("染色", dto.Name);
        Assert.Equal("后整理", dto.Category);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_BothNullCategory_Throws()
    {
        // Category=NULL 的同名：DB 唯一索引不拦截，应用层 spec 兜底
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate() with { Category = null });
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(ValidCreate() with { Category = null }));
    }

    // ── 编辑 ──

    [Fact]
    public async Task UpdateAsync_CodeImmutable_FieldsUpdatable()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "PRC-0001";
        var dto = await svc.CreateAsync(ValidCreate());
        await svc.UpdateAsync(dto.Id, new UpdateProcessRequest
        {
            Name = "染色改", Category = "染色", SortOrder = 5, Remark = "备注",
        });
        var updated = await svc.GetByIdAsync(dto.Id);
        Assert.Equal("PRC-0001", updated!.Code);   // code 由引擎生成，编辑不可改
        Assert.Equal("染色改", updated.Name);
        Assert.Equal("染色", updated.Category);
        Assert.Equal(5, updated.SortOrder);
        Assert.Equal("备注", updated.Remark);
    }

    [Fact]
    public async Task UpdateAsync_RenameToExisting_InSameCategory_Throws()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate() with { Name = "染色", Category = "前处理" });
        var b = await svc.CreateAsync(ValidCreate() with { Name = "织造", Category = "前处理", SortOrder = 2 });
        // 把 b 改名成已存在的「染色/前处理」→ 应抛错
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(b.Id, new UpdateProcessRequest { Name = "染色", Category = "前处理" }));
    }

    [Fact]
    public async Task UpdateAsync_KeepOwnName_Allowed()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.CreateAsync(ValidCreate());   // 染色/前处理
        // 改其它字段但保留同名同分类 → 不应触发查重（excludingId 生效）
        await svc.UpdateAsync(dto.Id, new UpdateProcessRequest
        {
            Name = "染色", Category = "前处理", SortOrder = 9,
        });
        var updated = await svc.GetByIdAsync(dto.Id);
        Assert.Equal(9, updated!.SortOrder);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(Guid.NewGuid(), new UpdateProcessRequest { Name = "x" }));
    }

    // ── 删除（软删除 + 幂等）──

    [Fact]
    public async Task DeleteAsync_SoftDeletes_AndIdempotent()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.CreateAsync(ValidCreate());
        await svc.DeleteAsync(dto.Id);
        // 软删后 GetByIdAsync（走软删过滤器）返回 null
        Assert.Null(await svc.GetByIdAsync(dto.Id));
        // 幂等重删不抛错（GetByIdAsync 走 FindAsync 绕过滤器）
        await svc.DeleteAsync(dto.Id);   // 不抛
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(() => svc.DeleteAsync(Guid.NewGuid()));
    }

    // ── 查询 ──

    [Fact]
    public async Task GetListAsync_FiltersByKeyword()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate() with { Name = "染色" });
        await svc.CreateAsync(ValidCreate() with { Name = "织造", SortOrder = 2 });
        var res = await svc.GetListAsync("染", null, null, 1, 10);
        Assert.Single(res.Items);
        Assert.Equal(1, res.Total);
    }

    [Fact]
    public async Task GetListAsync_FiltersByCategory()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate() with { Category = "前处理" });
        await svc.CreateAsync(ValidCreate() with { Category = "前处理", Name = "织造", SortOrder = 2 });
        await svc.CreateAsync(ValidCreate() with { Category = "后整理", Name = "定型", SortOrder = 3 });
        var res = await svc.GetListAsync(null, "前处理", null, 1, 10);
        Assert.Equal(2, res.Total);
    }

    [Fact]
    public async Task GetListAsync_FiltersByIsActive()
    {
        var (_, svc, _) = Setup();
        var a = await svc.CreateAsync(ValidCreate());
        await svc.UpdateAsync(a.Id, new UpdateProcessRequest
        { Name = "染色", Category = "前处理", IsActive = false });
        await svc.CreateAsync(ValidCreate() with { Name = "织造", SortOrder = 2 });
        var res = await svc.GetListAsync(null, null, false, 1, 10);
        Assert.Equal(1, res.Total);
        Assert.Equal("染色", res.Items[0].Name);
    }

    [Fact]
    public async Task GetListAsync_TotalUnaffectedByPaging()
    {
        var (_, svc, _) = Setup();
        for (var i = 0; i < 5; i++)
            await svc.CreateAsync(ValidCreate() with
            { Name = $"工序{i}", Category = "前处理", SortOrder = i });
        var res = await svc.GetListAsync(null, "前处理", null, 1, 2);
        Assert.Equal(2, res.Items.Count);
        Assert.Equal(5, res.Total);   // total 不受分页污染
    }

    [Fact]
    public async Task GetListAsync_OrdersBySortOrder()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate() with { Name = "C", SortOrder = 3 });
        await svc.CreateAsync(ValidCreate() with { Name = "A", SortOrder = 1 });
        await svc.CreateAsync(ValidCreate() with { Name = "B", SortOrder = 2 });
        var res = await svc.GetListAsync(null, null, null, 1, 10);
        Assert.Equal("A", res.Items[0].Name);
        Assert.Equal("B", res.Items[1].Name);
        Assert.Equal("C", res.Items[2].Name);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (_, svc, _) = Setup();
        Assert.Null(await svc.GetByIdAsync(Guid.NewGuid()));
    }
}

/// <summary>
/// 编号引擎测试替身：每次 GenerateAsync 返回自增 code（PRC-0001, PRC-0002 ...）。
/// NextCode 可显式覆盖首次返回值。
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
        return Task.FromResult($"PRC-{_seq:D4}");
    }

    public Task<string?> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
        => Task.FromResult<string?>(NextCode ?? $"PRC-{(_seq + 1):D4}");
}
