using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>
/// 仅按 keyword/targetType/isActive 过滤的编号规则查询规范(不含分页/排序)。
/// 专用于 CountAsync 统计符合条件的总数——若用带分页的规范统计,
/// Repository.CountAsync 会应用 Skip/Take,导致只统计当前页子集而非总数。
/// 与 <see cref="NumberingRulePagedSpec"/> 共享相同的 Where 条件,保证计数与数据一致。
/// </summary>
/// <remarks>
/// 注意:基类 <see cref="Specification{T}.ApplyCriteria"/> 是覆盖语义(Criteria = criteria),
/// 多次调用只保留最后一次。本规范含多个可选过滤条件,故必须组合为单一 predicate
/// 后调用 ApplyCriteria 一次。每个条件以"未设置时恒真(no-op)"哨兵守护。
/// </remarks>
public class NumberingRuleFilterSpec : Specification<NumberingRule>
{
    public NumberingRuleFilterSpec(string? keyword, string? targetType, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(r =>
            (kw == null || r.Name.Contains(kw) || r.Prefix.Contains(kw)) &&
            (string.IsNullOrEmpty(targetType) || r.TargetType == targetType) &&
            (isActive == null || r.IsActive == isActive.Value));
    }
}

/// <summary>编号规则分页查询(含 keyword/targetType/isActive 过滤、按创建时间倒序)。</summary>
/// <remarks>同 <see cref="NumberingRuleFilterSpec"/>:多条件组合为单一 predicate(见基类覆盖语义说明)。</remarks>
public class NumberingRulePagedSpec : Specification<NumberingRule>
{
    public NumberingRulePagedSpec(string? keyword, string? targetType, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(r =>
            (kw == null || r.Name.Contains(kw) || r.Prefix.Contains(kw)) &&
            (string.IsNullOrEmpty(targetType) || r.TargetType == targetType) &&
            (isActive == null || r.IsActive == isActive.Value));

        ApplyOrderByDescending(r => r.CreatedAt);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>按 Id 查询编号规则(tracked,详情/更新场景用 FirstOrDefaultAsync)。</summary>
public class NumberingRuleByIdSpec : Specification<NumberingRule>
{
    public NumberingRuleByIdSpec(Guid id)
    {
        ApplyCriteria(r => r.Id == id);
    }
}

/// <summary>
/// 唯一性校验:同 targetType 下是否已存在启用规则(可选排除自身 Id)。
/// 编号规则的唯一性是“同业务类型同时只能有一条启用规则”,而非字段 code 唯一。
/// </summary>
/// <remarks>
/// 基类 <see cref="Specification{T}.ApplyCriteria"/> 为覆盖语义,排除 Id 必须与
/// TargetType/IsActive 条件组合进同一次 ApplyCriteria 调用,否则后者会被覆盖,
/// 退化为“任意 Id≠自身 的规则存在即冲突”——导致合法编辑被误拒。
/// </remarks>
public class NumberingRuleActiveTargetTypeSpec : Specification<NumberingRule>
{
    public NumberingRuleActiveTargetTypeSpec(string targetType, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(r => r.TargetType == targetType && r.IsActive && (exclude == null || r.Id != exclude.Value));
    }
}
