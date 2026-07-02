using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Application.Services;
using OneCup.Infrastructure.Persistence;

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

    // ---------------------------------------------------------------------
    // 多条件组合过滤 (regression: ApplyCriteria 覆盖语义 bug)
    // Specification<T>.ApplyCriteria 是覆盖(Criteria = criteria)而非累加,
    // 旧实现中 keyword/targetType/isActive 各调一次 ApplyCriteria,只有最后一次存活。
    // 以下用例设置 ≥2 个过滤条件,确保所有条件都被 AND 组合(而非只剩最后一个)。
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_KeywordAndIsActive_BothApplied()
    {
        // bug:多 ApplyCriteria 下最后存活的条件是 isActive(原序 keyword→targetType→isActive)。
        // 故 bug 会返回所有 active 规则(忽略 keyword)=2 条;正确应只返回同时命中 keyword+isActive=1 条。
        // 注意:不同 TargetType 的 distractor 规避了"同 targetType 唯一启用"约束。
        var (db, svc) = Setup();
        await svc.CreateAsync(MakeCreate() with { TargetType = "fabric", Name = "面料A" });   // ✅ active + 命中 keyword
        await svc.CreateAsync(MakeCreate() with { TargetType = "material", Name = "其他" });  // active 但不命中 keyword

        var result = await svc.GetListAsync(1, 10, "面料", null, true);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Contains("面料", result.Items.Single().Name);
    }

    [Fact]
    public async Task GetListAsync_KeywordAndTargetType_BothApplied()
    {
        // bug:此组合(keyword+targetType,无 isActive)下最后存活的是 targetType。
        // 故 bug 返回所有 fabric 规则(含停用的 distractor)=2 条;正确应只返回命中 keyword 的=1 条。
        // 注意:同一 targetType 同时只能有一条启用规则,故 distractor 用"先建后停用"获得。
        var (db, svc) = Setup();
        var distractor = await svc.CreateAsync(MakeCreate() with { Name = "无关" });          // fabric active
        await svc.UpdateStatusAsync(distractor.Id, false);                                    // fabric 停用
        await svc.CreateAsync(MakeCreate() with { Name = "面料" });                           // ✅ fabric active + 命中 keyword

        var result = await svc.GetListAsync(1, 10, "面料", "fabric", null);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Contains("面料", result.Items.Single().Name);
    }

    [Fact]
    public async Task GetListAsync_KeywordAndTargetTypeAndIsActive_AllApplied()
    {
        // 三条件全开:只有同时满足 keyword+targetType+isActive 的规则才返回。
        // bug 下只剩 isActive → 返回所有 active 规则(2 条),而非正确的 1 条。
        // 注意唯一性约束:同一 targetType 同时只能有一条启用规则,故无法构造
        // "命中 targetType+isActive 但 keyword 不同"的启用规则(会违反唯一性)。
        // 仍能区分 bug 与修复:bug 返回 rule1 + distractorB(active)=2,修复返回 1。
        var (db, svc) = Setup();
        // 先建 fabric 启用规则,随即停用——作为 distractorC(命中 keyword+targetType 但停用)
        var m2 = await svc.CreateAsync(MakeCreate() with { TargetType = "fabric", Name = "面料", Prefix = "FB2" });
        await svc.UpdateStatusAsync(m2.Id, false);
        // ✅ 匹配:fabric + active + name 含 "面料"
        await svc.CreateAsync(MakeCreate() with { TargetType = "fabric", Name = "面料" });   // prefix=FAB
        // distractorB:命中 keyword+isActive 但 targetType 不同
        await svc.CreateAsync(MakeCreate() with { TargetType = "material", Name = "面料", Prefix = "MT3" });

        var result = await svc.GetListAsync(1, 10, "面料", "fabric", true);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("FAB", result.Items.Single().Prefix); // 匹配规则的 Prefix
    }

    // ---------------------------------------------------------------------
    // 唯一性校验 excludingId 与无关 TargetType (regression: ApplyCriteria 覆盖 bug)
    // 旧 NumberingRuleActiveTargetTypeSpec 先 ApplyCriteria(TargetType&&IsActive),
    // 再 ApplyCriteria(Id != excludingId) 覆盖前者 → 退化为"任意 Id≠自身 的规则存在"。
    // 当存在一条无关 TargetType 的启用规则时,会误判冲突,拒绝合法操作。
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UpdateStatusAsync_Reactivate_WithUnrelatedActiveRule_DoesNotThrow()
    {
        // rule1(fabric) 停用;rule2(material,不同 TargetType)启用。
        // 重新启用 rule1 时,唯一性校验应只看 fabric,忽略 material 的 rule2。
        var (db, svc) = Setup();
        var rule1 = await svc.CreateAsync(MakeCreate() with { TargetType = "fabric" });
        await svc.UpdateStatusAsync(rule1.Id, false);
        await svc.CreateAsync(MakeCreate() with { TargetType = "material", Name = "物料", Prefix = "MAT" });

        // 不应抛出:material 规则与 fabric 无关。
        await svc.UpdateStatusAsync(rule1.Id, true);

        var r = await svc.GetAsync(rule1.Id);
        Assert.True(r!.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_KeepTargetType_WithUnrelatedActiveRule_DoesNotThrow()
    {
        // rule1(fabric,已停用)更新自身;存在无关 material 启用规则。
        // 触发 TargetType 非空时的唯一性复检,应排除自身后只看 fabric,不误判冲突。
        var (db, svc) = Setup();
        var rule1 = await svc.CreateAsync(MakeCreate() with { TargetType = "fabric" });
        await svc.UpdateStatusAsync(rule1.Id, false);
        await svc.CreateAsync(MakeCreate() with { TargetType = "material", Name = "物料", Prefix = "MAT" });

        // 重新提交相同 TargetType(同时改备注),应成功。
        await svc.UpdateAsync(rule1.Id, new UpdateNumberingRuleRequest
        {
            TargetType = "fabric",
            Remark = "更新备注",
        });

        var r = await svc.GetAsync(rule1.Id);
        Assert.Equal("更新备注", r!.Remark);
    }
}
