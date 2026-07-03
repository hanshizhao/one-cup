using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Application.Services;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.NumberingDictionary;

public class NumberingDictionaryServiceTests
{
    private static (OneCupDbContext db, NumberingDictionaryService svc) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"numbering-dict-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        var svc = new NumberingDictionaryService(
            new Repository<NumberingTargetType>(db),
            new Repository<NumberingCategory>(db),
            new UnitOfWork(db));
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    // ── 业务类型 ──

    [Fact]
    public async Task CreateTargetTypeAsync_CreatesType()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "order", NameZh = "订单", NameEn = "Order", SortOrder = 10,
        });
        Assert.Equal("order", dto.Code);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task CreateTargetTypeAsync_DuplicateCode_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "order", NameZh = "订单", NameEn = "Order",
        });
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
            {
                Code = "order", NameZh = "订单2", NameEn = "Order2",
            }));
    }

    [Fact]
    public async Task UpdateTargetTypeAsync_CodeIgnored()
    {
        // code 不可改：更新时即使传了新 code 也应忽略
        var (db, svc) = Setup();
        var dto = await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "order", NameZh = "订单", NameEn = "Order",
        });
        await svc.UpdateTargetTypeAsync(dto.Id, new UpdateTargetTypeRequest
        {
            NameZh = "订单改", NameEn = "Order改",
        });
        var updated = await svc.GetTargetTypeAsync(dto.Id);
        Assert.Equal("order", updated!.Code);        // code 不变
        Assert.Equal("订单改", updated.NameZh);
    }

    [Fact]
    public async Task UpdateTargetTypeStatusAsync_Toggles()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "order", NameZh = "订单", NameEn = "Order",
        });
        await svc.UpdateTargetTypeStatusAsync(dto.Id, false);
        var updated = await svc.GetTargetTypeAsync(dto.Id);
        Assert.False(updated!.IsActive);
    }

    // ── 分类 ──

    [Fact]
    public async Task CreateCategoryAsync_RequiresExistingTargetType()
    {
        var (db, svc) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateCategoryAsync(new CreateCategoryRequest
            {
                TargetTypeCode = "nonexistent", Code = "COT", NameZh = "棉", NameEn = "Cotton",
            }));
    }

    [Fact]
    public async Task CreateCategoryAsync_CreatesCategory()
    {
        var (db, svc) = Setup();
        await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "fabric", NameZh = "面料", NameEn = "Fabric",
        });
        var dto = await svc.CreateCategoryAsync(new CreateCategoryRequest
        {
            TargetTypeCode = "fabric", Code = "COT", NameZh = "棉", NameEn = "Cotton",
        });
        Assert.Equal("COT", dto.Code);
        Assert.Equal("fabric", dto.TargetTypeCode);
    }

    [Fact]
    public async Task CreateCategoryAsync_DuplicateInSameType_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "fabric", NameZh = "面料", NameEn = "Fabric",
        });
        await svc.CreateCategoryAsync(new CreateCategoryRequest
        {
            TargetTypeCode = "fabric", Code = "COT", NameZh = "棉", NameEn = "Cotton",
        });
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateCategoryAsync(new CreateCategoryRequest
            {
                TargetTypeCode = "fabric", Code = "COT", NameZh = "棉2", NameEn = "Cotton2",
            }));
    }

    [Fact]
    public async Task CreateCategoryAsync_SameCodeDifferentType_Allowed()
    {
        // COT 在 fabric 下有了，material 下也可有 COT
        var (db, svc) = Setup();
        await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "fabric", NameZh = "面料", NameEn = "Fabric",
        });
        await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "material", NameZh = "原料", NameEn = "Material",
        });
        await svc.CreateCategoryAsync(new CreateCategoryRequest
        {
            TargetTypeCode = "fabric", Code = "COT", NameZh = "棉", NameEn = "Cotton",
        });
        // 不同 targetTypeCode 下同 code 不冲突
        await svc.CreateCategoryAsync(new CreateCategoryRequest
        {
            TargetTypeCode = "material", Code = "COT", NameZh = "棉纱", NameEn = "Cotton Yarn",
        });
    }

    [Fact]
    public async Task GetActiveCategoriesAsync_ReturnsOnlyActive()
    {
        var (db, svc) = Setup();
        await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "fabric", NameZh = "面料", NameEn = "Fabric",
        });
        var c1 = await svc.CreateCategoryAsync(new CreateCategoryRequest
        {
            TargetTypeCode = "fabric", Code = "COT", NameZh = "棉", NameEn = "Cotton", SortOrder = 1,
        });
        await svc.CreateCategoryAsync(new CreateCategoryRequest
        {
            TargetTypeCode = "fabric", Code = "POL", NameZh = "涤纶", NameEn = "Polyester", SortOrder = 2,
        });
        await svc.UpdateCategoryStatusAsync(c1.Id, false);

        var list = await svc.GetActiveCategoriesAsync("fabric");
        Assert.Single(list);
        Assert.Equal("POL", list[0].Code);
    }
}
