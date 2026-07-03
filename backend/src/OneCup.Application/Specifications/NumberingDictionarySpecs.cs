using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

// ── 业务类型规格 ──

/// <summary>业务类型过滤规格（仅 keyword/isActive，不含分页）。用于 CountAsync 统计总数。</summary>
/// <remarks>多条件必须组合为单一 predicate 调一次 ApplyCriteria（基类覆盖语义，见 NumberingRuleFilterSpec 说明）。</remarks>
public class TargetTypeFilterSpec : Specification<NumberingTargetType>
{
    public TargetTypeFilterSpec(string? keyword, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(t =>
            (kw == null || t.Code.Contains(kw) || t.NameZh.Contains(kw) || t.NameEn.Contains(kw)) &&
            (isActive == null || t.IsActive == isActive.Value));
    }
}

/// <summary>业务类型分页查询（含 keyword/isActive 过滤，按 SortOrder 升序）。</summary>
public class TargetTypePagedSpec : Specification<NumberingTargetType>
{
    public TargetTypePagedSpec(string? keyword, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(t =>
            (kw == null || t.Code.Contains(kw) || t.NameZh.Contains(kw) || t.NameEn.Contains(kw)) &&
            (isActive == null || t.IsActive == isActive.Value));
        ApplyOrderBy(t => t.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>业务类型全部启用项（前端下拉用，按 SortOrder 升序）。</summary>
public class TargetTypeActiveSpec : Specification<NumberingTargetType>
{
    public TargetTypeActiveSpec()
    {
        ApplyCriteria(t => t.IsActive);
        ApplyOrderBy(t => t.SortOrder);
    }
}

public class TargetTypeByIdSpec : Specification<NumberingTargetType>
{
    public TargetTypeByIdSpec(Guid id) => ApplyCriteria(t => t.Id == id);
}

/// <summary>按 code 查找业务类型（可选排除自身 Id）。不含 IsActive 过滤——
/// 用于 code 唯一性校验时正确（停用也占用了 code）。</summary>
public class TargetTypeByCodeSpec : Specification<NumberingTargetType>
{
    public TargetTypeByCodeSpec(string code, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(t => t.Code == code && (exclude == null || t.Id != exclude.Value));
    }
}

/// <summary>按 code 查找存在且启用的业务类型。
/// 用于"校验业务类型存在且启用"场景（引擎校验、分类新增前的合法性校验）。</summary>
public class TargetTypeActiveByCodeSpec : Specification<NumberingTargetType>
{
    public TargetTypeActiveByCodeSpec(string code)
    {
        ApplyCriteria(t => t.Code == code && t.IsActive);
    }
}

// ── 分类规格 ──

/// <summary>分类过滤规格（仅 targetTypeCode/keyword/isActive，不含分页）。用于 CountAsync。</summary>
public class CategoryFilterSpec : Specification<NumberingCategory>
{
    public CategoryFilterSpec(string? targetTypeCode, string? keyword, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(c =>
            (string.IsNullOrEmpty(targetTypeCode) || c.TargetTypeCode == targetTypeCode) &&
            (kw == null || c.Code.Contains(kw) || c.NameZh.Contains(kw) || c.NameEn.Contains(kw)) &&
            (isActive == null || c.IsActive == isActive.Value));
    }
}

/// <summary>分类分页查询（含 targetTypeCode/keyword/isActive，按 SortOrder 升序）。</summary>
public class CategoryPagedSpec : Specification<NumberingCategory>
{
    public CategoryPagedSpec(string? targetTypeCode, string? keyword, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(c =>
            (string.IsNullOrEmpty(targetTypeCode) || c.TargetTypeCode == targetTypeCode) &&
            (kw == null || c.Code.Contains(kw) || c.NameZh.Contains(kw) || c.NameEn.Contains(kw)) &&
            (isActive == null || c.IsActive == isActive.Value));
        ApplyOrderBy(c => c.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>某业务类型下全部启用分类（前端联动下拉用，按 SortOrder 升序）。</summary>
public class CategoryActiveByTypeSpec : Specification<NumberingCategory>
{
    public CategoryActiveByTypeSpec(string targetTypeCode)
    {
        ApplyCriteria(c => c.TargetTypeCode == targetTypeCode && c.IsActive);
        ApplyOrderBy(c => c.SortOrder);
    }
}

public class CategoryByIdSpec : Specification<NumberingCategory>
{
    public CategoryByIdSpec(Guid id) => ApplyCriteria(c => c.Id == id);
}

/// <summary>分类唯一性校验：(targetTypeCode, code) 组合唯一，可选排除自身 Id。</summary>
public class CategoryByTypeAndCodeSpec : Specification<NumberingCategory>
{
    public CategoryByTypeAndCodeSpec(string targetTypeCode, string code, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(c => c.TargetTypeCode == targetTypeCode && c.Code == code
                        && (exclude == null || c.Id != exclude.Value));
    }
}
