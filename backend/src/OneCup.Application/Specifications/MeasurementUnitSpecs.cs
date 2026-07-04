using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

// ── 过滤/分页 ──

/// <summary>单位过滤规格（仅 keyword/category/isActive，不含分页）。用于 CountAsync 统计总数。</summary>
public class UnitFilterSpec : Specification<MeasurementUnit>
{
    public UnitFilterSpec(string? keyword, string? category, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(u =>
            (kw == null || u.Code.Contains(kw) || u.NameZh.Contains(kw) || u.NameEn.Contains(kw)) &&
            (string.IsNullOrEmpty(category) || u.Category == category) &&
            (isActive == null || u.IsActive == isActive.Value));
    }
}

/// <summary>单位分页查询（含过滤，按 SortOrder、Code 升序）。</summary>
public class UnitPagedSpec : Specification<MeasurementUnit>
{
    public UnitPagedSpec(string? keyword, string? category, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(u =>
            (kw == null || u.Code.Contains(kw) || u.NameZh.Contains(kw) || u.NameEn.Contains(kw)) &&
            (string.IsNullOrEmpty(category) || u.Category == category) &&
            (isActive == null || u.IsActive == isActive.Value));
        ApplyOrderBy(u => u.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>全部启用单位（前端下拉用，按 SortOrder 升序）。</summary>
public class UnitActiveSpec : Specification<MeasurementUnit>
{
    public UnitActiveSpec()
    {
        ApplyCriteria(u => u.IsActive);
        ApplyOrderBy(u => u.SortOrder);
    }
}

// ── 查找/唯一性 ──

public class UnitByIdSpec : Specification<MeasurementUnit>
{
    public UnitByIdSpec(Guid id) => ApplyCriteria(u => u.Id == id);
}

/// <summary>按 code 查找单位（不含 IsActive 过滤——用于 code 唯一性校验与 ConvertAsync 区分"不存在/已停用"）。</summary>
public class UnitByCodeSpec : Specification<MeasurementUnit>
{
    public UnitByCodeSpec(string code, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(u => u.Code == code && (exclude == null || u.Id != exclude.Value));
    }
}

/// <summary>查找某 category 当前的基准单位（可选排除自身 Id）。用于基准唯一性校验。</summary>
public class UnitBaseByCategorySpec : Specification<MeasurementUnit>
{
    public UnitBaseByCategorySpec(string category, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(u => u.Category == category && u.IsBase && (exclude == null || u.Id != exclude.Value));
    }
}
