using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>
/// 仅按 keyword/targetType/isActive 过滤的编号规则查询规范(不含分页/排序)。
/// 专用于 CountAsync 统计符合条件的总数——若用带分页的规范统计,
/// Repository.CountAsync 会应用 Skip/Take,导致只统计当前页子集而非总数。
/// 与 <see cref="NumberingRulePagedSpec"/> 共享相同的 Where 条件,保证计数与数据一致。
/// </summary>
public class NumberingRuleFilterSpec : Specification<NumberingRule>
{
    public NumberingRuleFilterSpec(string? keyword, string? targetType, bool? isActive)
    {
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim();
            ApplyCriteria(r => r.Name.Contains(keyword) || r.Prefix.Contains(keyword));
        }
        if (!string.IsNullOrEmpty(targetType))
            ApplyCriteria(r => r.TargetType == targetType);
        if (isActive is not null)
            ApplyCriteria(r => r.IsActive == isActive);
    }
}

/// <summary>编号规则分页查询(含 keyword/targetType/isActive 过滤、按创建时间倒序)。</summary>
public class NumberingRulePagedSpec : Specification<NumberingRule>
{
    public NumberingRulePagedSpec(string? keyword, string? targetType, bool? isActive, int page, int pageSize)
    {
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim();
            ApplyCriteria(r => r.Name.Contains(keyword) || r.Prefix.Contains(keyword));
        }
        if (!string.IsNullOrEmpty(targetType))
            ApplyCriteria(r => r.TargetType == targetType);
        if (isActive is not null)
            ApplyCriteria(r => r.IsActive == isActive);

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
public class NumberingRuleActiveTargetTypeSpec : Specification<NumberingRule>
{
    public NumberingRuleActiveTargetTypeSpec(string targetType, Guid? excludingId = null)
    {
        ApplyCriteria(r => r.TargetType == targetType && r.IsActive);
        if (excludingId is not null)
            ApplyCriteria(r => r.Id != excludingId!.Value);
    }
}
