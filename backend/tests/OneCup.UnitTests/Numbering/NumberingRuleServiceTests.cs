using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Application.Services;
using OneCup.Infrastructure.Persistence;
using OneCup.Infrastructure.Services;

namespace OneCup.UnitTests.Numbering;

public class NumberingRuleServiceTests
{
    private static (OneCupDbContext db, NumberingRuleService svc) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"numbering-rule-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        var svc = new NumberingRuleService(
            new Repository<NumberingRule>(db),
            new Repository<NumberingLog>(db),
            new UnitOfWork(db),
            new NumberingClock());
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static CreateNumberingRuleRequest MakeCreate() => new()
    {
        TargetType = "fabric",
        Name = "面料编码",
        Prefix = "FAB",
        IncludeCategory = true,
        DateSegment = DateSegment.Year,
        SeqLength = 4,
        Separator = "-",
        ResetPeriod = ResetPeriod.Yearly,
    };

    [Fact]
    public async Task CreateAsync_CreatesRule()
    {
        var (db, svc) = Setup();
        var rule = await svc.CreateAsync(MakeCreate());
        Assert.Equal("fabric", rule.TargetType);
        Assert.True(rule.IsActive);
        Assert.Contains("FAB-", rule.SampleFormat);
    }

    [Fact]
    public async Task CreateAsync_PrefixContainsSeparator_Throws()
    {
        var (db, svc) = Setup();
        var req = MakeCreate() with { Prefix = "FA-B", Separator = "-" };
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(req));
    }

    [Fact]
    public async Task CreateAsync_DuplicateActiveTargetType_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(MakeCreate());
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(MakeCreate()));
    }

    [Fact]
    public async Task UpdateAsync_LockedFieldsWhenActive_Throws()
    {
        var (db, svc) = Setup();
        var rule = await svc.CreateAsync(MakeCreate());
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(rule.Id, new UpdateNumberingRuleRequest { Prefix = "FABRIC" }));
        Assert.Contains("停用", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_RemarkAllowedWhenActive()
    {
        var (db, svc) = Setup();
        var rule = await svc.CreateAsync(MakeCreate());
        await svc.UpdateAsync(rule.Id, new UpdateNumberingRuleRequest { Remark = "测试备注" });
        var updated = await svc.GetAsync(rule.Id);
        Assert.Equal("测试备注", updated!.Remark);
    }

    [Fact]
    public async Task UpdateStatusAsync_DeactivateAllowsEdit()
    {
        var (db, svc) = Setup();
        var rule = await svc.CreateAsync(MakeCreate());
        await svc.UpdateStatusAsync(rule.Id, false);
        // 停用后应可改关键字段
        await svc.UpdateAsync(rule.Id, new UpdateNumberingRuleRequest { Prefix = "FABRIC" });
        var updated = await svc.GetAsync(rule.Id);
        Assert.Equal("FABRIC", updated!.Prefix);
    }

    [Fact]
    public async Task UpdateStatusAsync_ActivateDuplicateTargetType_Throws()
    {
        var (db, svc) = Setup();
        var rule1 = await svc.CreateAsync(MakeCreate());
        await svc.UpdateStatusAsync(rule1.Id, false);
        var rule2 = await svc.CreateAsync(MakeCreate());
        // rule2 已是启用，尝试启用 rule1 应冲突
        await Assert.ThrowsAsync<DomainException>(() => svc.UpdateStatusAsync(rule1.Id, true));
    }

    [Fact]
    public async Task GetListAsync_FiltersByTargetType()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(MakeCreate() with { TargetType = "material", Name = "物料" });
        var result = await svc.GetListAsync(1, 10, null, "material", null);
        Assert.Single(result.Items);
    }
}
