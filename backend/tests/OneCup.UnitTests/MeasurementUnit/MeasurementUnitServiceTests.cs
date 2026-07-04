using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Services;
using OneCup.Application.Validators.System;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.MeasurementUnit;

public class MeasurementUnitServiceTests
{
    private static (OneCupDbContext db, MeasurementUnitService svc) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"unit-test-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        var svc = new MeasurementUnitService(
            new Repository<Domain.Entities.MeasurementUnit>(db),
            new UnitOfWork(db),
            new CreateUnitRequestValidator());
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static CreateUnitRequest ValidCreate(string code, string category, bool isBase, decimal factor = 1m) => new()
    {
        Code = code, NameZh = code, NameEn = code, Symbol = code,
        Category = category, IsBase = isBase, Factor = factor,
        Precision = 2, SortOrder = 1,
    };

    // ── Create ──

    [Fact]
    public async Task CreateAsync_CreatesUnit()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        Assert.Equal("meter", dto.Code);
        Assert.True(dto.IsActive);
        Assert.True(dto.IsBase);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(ValidCreate("meter", "LENGTH", false, 0.1m)));
    }

    [Fact]
    public async Task CreateAsync_BaseFactor_ForcedToOne()
    {
        var (db, svc) = Setup();
        // 即使传 factor=5，IsBase=true 时强制为 1
        var dto = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true, 5m));
        Assert.Equal(1m, dto.Factor);
    }

    [Fact]
    public async Task CreateAsync_SecondBaseInCategory_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(ValidCreate("yard", "LENGTH", true)));
    }

    [Fact]
    public async Task CreateAsync_NonBase_InDifferentCategory_Ok()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        // 不同 category 各自一个基准，不冲突
        var dto = await svc.CreateAsync(ValidCreate("kg", "WEIGHT", true));
        Assert.True(dto.IsBase);
    }

    // ── Update ──

    [Fact]
    public async Task UpdateAsync_BaseFactorChange_Throws()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(dto.Id, new UpdateUnitRequest { Factor = 2m }));
    }

    [Fact]
    public async Task UpdateAsync_NonBaseFactorChange_Ok()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        var yard = await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9m));
        await svc.UpdateAsync(yard.Id, new UpdateUnitRequest { Factor = 0.9144m });
        var updated = await svc.GetByIdAsync(yard.Id);
        Assert.Equal(0.9144m, updated!.Factor);
    }

    [Fact]
    public async Task UpdateAsync_SwitchBase_DemotesOldBase()
    {
        var (db, svc) = Setup();
        var meter = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        var yard = await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9144m));
        // 把 yard 设为新基准 → meter 自动降级
        await svc.UpdateAsync(yard.Id, new UpdateUnitRequest { IsBase = true });
        var oldBase = await svc.GetByIdAsync(meter.Id);
        var newBase = await svc.GetByIdAsync(yard.Id);
        Assert.False(oldBase!.IsBase);
        Assert.True(newBase!.IsBase);
        Assert.Equal(1m, newBase.Factor);
    }

    [Fact]
    public async Task UpdateAsync_RemoveLastBase_Throws()
    {
        var (db, svc) = Setup();
        var meter = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(meter.Id, new UpdateUnitRequest { IsBase = false }));
    }

    [Fact]
    public async Task UpdateAsync_ZeroFactor_Throws()
    {
        // 防止 factor=0 被持久化后导致 ConvertAsync 触发 DivideByZeroException（HTTP 500）
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        var yard = await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9m));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(yard.Id, new UpdateUnitRequest { Factor = 0m }));
    }

    [Fact]
    public async Task UpdateAsync_PrecisionOutOfRange_Throws()
    {
        // Precision 必须在 0-6 之间（与 Create 校验一致）
        var (db, svc) = Setup();
        var meter = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(meter.Id, new UpdateUnitRequest { Precision = 99 }));
    }

    // ── UpdateStatus ──

    [Fact]
    public async Task UpdateStatusAsync_DeactivateBase_Throws()
    {
        var (db, svc) = Setup();
        var meter = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateStatusAsync(meter.Id, false));
    }

    [Fact]
    public async Task UpdateStatusAsync_DeactivateNonBase_Ok()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        var yard = await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9144m));
        await svc.UpdateStatusAsync(yard.Id, false);
        var updated = await svc.GetByIdAsync(yard.Id);
        Assert.False(updated!.IsActive);
    }

    // ── Convert ──

    [Fact]
    public async Task ConvertAsync_SameCategory_Ok()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9144m));
        var result = await svc.ConvertAsync(new ConvertUnitRequest
        {
            FromCode = "yard", ToCode = "meter", Quantity = 10m,
        });
        // 10 yard × 0.9144 / 1 = 9.144，按目标单位 meter 的 precision=2 四舍五入 → 9.14
        Assert.Equal(9.14m, result.Quantity);
    }

    [Fact]
    public async Task ConvertAsync_DifferentCategory_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await svc.CreateAsync(ValidCreate("kg", "WEIGHT", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.ConvertAsync(new ConvertUnitRequest
            {
                FromCode = "meter", ToCode = "kg", Quantity = 1m,
            }));
    }

    [Fact]
    public async Task ConvertAsync_DeactivatedUnit_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        var yard = await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9144m));
        await svc.UpdateStatusAsync(yard.Id, false);
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.ConvertAsync(new ConvertUnitRequest
            {
                FromCode = "yard", ToCode = "meter", Quantity = 1m,
            }));
    }

    [Fact]
    public async Task ConvertAsync_NonExistent_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.ConvertAsync(new ConvertUnitRequest
            {
                FromCode = "meter", ToCode = "nonexistent", Quantity = 1m,
            }));
    }

    [Fact]
    public async Task ConvertAsync_CountClass_ResultUnchanged()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("piece", "COUNT", true));
        await svc.CreateAsync(ValidCreate("roll", "COUNT", false, 1m));
        var result = await svc.ConvertAsync(new ConvertUnitRequest
        {
            FromCode = "piece", ToCode = "roll", Quantity = 10m,
        });
        // COUNT 类 factor 都为 1 → 结果不变
        Assert.Equal(10m, result.Quantity);
    }

    [Fact]
    public async Task ConvertAsync_YarnClass_Ok()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("tex", "YARN", true));
        await svc.CreateAsync(ValidCreate("denier", "YARN", false, 9m));
        // 10 denier → tex = 10 × 9 / 1 = 90
        var r1 = await svc.ConvertAsync(new ConvertUnitRequest
        {
            FromCode = "denier", ToCode = "tex", Quantity = 10m,
        });
        Assert.Equal(90m, r1.Quantity);
        // 10 tex → denier = 10 × 1 / 9 = 1.11（precision=2）
        var r2 = await svc.ConvertAsync(new ConvertUnitRequest
        {
            FromCode = "tex", ToCode = "denier", Quantity = 10m,
        });
        Assert.Equal(1.11m, r2.Quantity);
    }

    // ── GetCategories ──

    [Fact]
    public async Task GetCategoriesAsync_ReturnsDistinctActive()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9144m));
        await svc.CreateAsync(ValidCreate("kg", "WEIGHT", true));
        var cats = await svc.GetCategoriesAsync();
        Assert.Equal(2, cats.Count);
        Assert.Contains("LENGTH", cats);
        Assert.Contains("WEIGHT", cats);
    }
}
