using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Services;
using OneCup.Application.Specifications;
using OneCup.Application.Validators.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
// 本测试位于命名空间 OneCup.UnitTests.Customer，未限定的 "Customer" 会绑定到命名空间，
// 故用别名显式指向实体类型，避免 CS0118（命名空间被当作类型）。
using CustomerEntity = OneCup.Domain.Entities.Customer;

namespace OneCup.UnitTests.Customer;

public class CustomerServiceTests
{
    private static (OneCupDbContext db, CustomerService svc, FakeNumberingService numbering) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"customer-test-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);

        var numbering = new FakeNumberingService();
        var svc = new CustomerService(
            new Repository<CustomerEntity>(db),
            new UnitOfWork(db),
            numbering,
            new CreateCustomerRequestValidator(),
            new UpdateCustomerRequestValidator());
        return (db, svc, numbering);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateAsync_CreatesCustomer_WithGeneratedCode()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "CUST-0007";

        var result = await svc.CreateAsync(new CreateCustomerRequest
        {
            Name = "深圳XX服饰",
            IsActive = true,
        });

        Assert.Equal("CUST-0007", result.Code);
        Assert.Equal("深圳XX服饰", result.Name);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        var (db, svc, _) = Setup();
        db.Customers.Add(new CustomerEntity { Code = "C1", Name = "已存在客户" });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(new CreateCustomerRequest { Name = "已存在客户" }));
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_IgnoresSoftDeleted_Throws()
    {
        // 已软删除客户占用的名称，新建时仍应被识别为占用
        var (db, svc, _) = Setup();
        db.Customers.Add(new CustomerEntity { Code = "C1", Name = "软删客户", IsDeleted = true });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(new CreateCustomerRequest { Name = "软删客户" }));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields()
    {
        var (db, svc, _) = Setup();
        var c = new CustomerEntity { Code = "C1", Name = "原名", IsActive = true };
        db.Customers.Add(c);
        await db.SaveChangesAsync();

        var result = await svc.UpdateAsync(c.Id, new UpdateCustomerRequest
        {
            Name = "原名",
            ShortName = "新简称",
            IsActive = false,
        });

        Assert.Equal("新简称", result.ShortName);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_KeepOwnName_Allowed()
    {
        var (db, svc, _) = Setup();
        var c = new CustomerEntity { Code = "C1", Name = "独一名", IsActive = true };
        db.Customers.Add(c);
        await db.SaveChangesAsync();

        var result = await svc.UpdateAsync(c.Id, new UpdateCustomerRequest
        {
            Name = "独一名",  // 不改名
            IsActive = true,
        });

        Assert.Equal("独一名", result.Name);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateName_OnOther_Throws()
    {
        var (db, svc, _) = Setup();
        var a = new CustomerEntity { Code = "C1", Name = "客户A" };
        var b = new CustomerEntity { Code = "C2", Name = "客户B" };
        db.Customers.AddRange(a, b);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(b.Id, new UpdateCustomerRequest { Name = "客户A" }));
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        var (db, svc, _) = Setup();
        var c = new CustomerEntity { Code = "C1", Name = "待删", IsActive = true };
        db.Customers.Add(c);
        await db.SaveChangesAsync();

        await svc.DeleteAsync(c.Id);

        // 全局查询过滤器会隐藏软删记录，用 IgnoreQueryFilters 验证
        var soft = await db.Customers.IgnoreQueryFilters().FirstAsync(x => x.Id == c.Id);
        Assert.True(soft.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(() => svc.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_Idempotent_SecondDeleteDoesNotThrow()
    {
        var (db, svc, _) = Setup();
        var c = new CustomerEntity { Code = "C1", Name = "待删", IsActive = true };
        db.Customers.Add(c);
        await db.SaveChangesAsync();

        await svc.DeleteAsync(c.Id);        // 第一次删除：成功
        await svc.DeleteAsync(c.Id);        // 第二次删除：幂等，不应抛异常

        // 验证仍处于软删状态
        var soft = await db.Customers.IgnoreQueryFilters().FirstAsync(x => x.Id == c.Id);
        Assert.True(soft.IsDeleted);
    }

    [Fact]
    public async Task GetListAsync_AppliesFilters()
    {
        var (db, svc, _) = Setup();
        db.Customers.AddRange(
            new CustomerEntity { Code = "C1", Name = "甲客户", IsActive = true },
            new CustomerEntity { Code = "C2", Name = "乙客户", IsActive = false });
        await db.SaveChangesAsync();

        var result = await svc.GetListAsync("甲", null, null, 1, 10);
        Assert.Single(result.Items);
        Assert.Equal("甲客户", result.Items[0].Name);
    }

    [Fact]
    public async Task GetListAsync_ExcludesSoftDeleted()
    {
        var (db, svc, _) = Setup();
        db.Customers.AddRange(
            new CustomerEntity { Code = "C1", Name = "可见", IsActive = true },
            new CustomerEntity { Code = "C2", Name = "已删", IsActive = true, IsDeleted = true });
        await db.SaveChangesAsync();

        var result = await svc.GetListAsync(null, null, null, 1, 10);
        Assert.Single(result.Items);
        Assert.Equal("可见", result.Items[0].Name);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (_, svc, _) = Setup();
        var result = await svc.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }
}

/// <summary>编号服务测试桩：返回固定编号，不碰事务/行锁。</summary>
/// <remarks>
/// 不能用 C# 11 <c>file class</c>：file-local 类型不能出现在非 file-local 类型
/// <see cref="CustomerServiceTests"/> 的成员签名（<see cref="Setup"/> 返回元组）中（CS9051），
/// 故降级为程序集内部类。
/// </remarks>
internal sealed class FakeNumberingService : INumberingService
{
    public string NextCode { get; set; } = "CUST-0001";
    public Task<string> GenerateAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
        => Task.FromResult(NextCode);
    public Task<PreviewResult> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
        => Task.FromResult(new PreviewResult { Code = NextCode, IncludeCategory = false });
}
