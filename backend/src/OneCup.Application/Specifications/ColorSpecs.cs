using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>颜色过滤规格（仅 keyword/colorFamily/isActive，不含分页）。用于 CountAsync 统计总数。</summary>
/// <remarks>多条件组合为单一 predicate 调一次 ApplyCriteria（基类覆盖语义，见 NumberingRuleFilterSpec 说明）。</remarks>
public class ColorFilterSpec : Specification<Color>
{
    public ColorFilterSpec(string? keyword, string? colorFamily, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var fam = string.IsNullOrWhiteSpace(colorFamily) ? null : colorFamily.Trim();
        ApplyCriteria(c =>
            (kw == null || c.Code.Contains(kw) || c.NameZh.Contains(kw) || c.NameEn.Contains(kw)) &&
            (fam == null || c.ColorFamily == fam) &&
            (isActive == null || c.IsActive == isActive.Value));
    }
}

/// <summary>颜色分页查询（含 keyword/colorFamily/isActive 过滤，按 SortOrder 升序）。</summary>
public class ColorPagedSpec : Specification<Color>
{
    public ColorPagedSpec(string? keyword, string? colorFamily, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var fam = string.IsNullOrWhiteSpace(colorFamily) ? null : colorFamily.Trim();
        ApplyCriteria(c =>
            (kw == null || c.Code.Contains(kw) || c.NameZh.Contains(kw) || c.NameEn.Contains(kw)) &&
            (fam == null || c.ColorFamily == fam) &&
            (isActive == null || c.IsActive == isActive.Value));
        ApplyOrderBy(c => c.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>颜色全部启用项（前端下拉用，按 SortOrder 升序）。</summary>
public class ColorActiveSpec : Specification<Color>
{
    public ColorActiveSpec()
    {
        ApplyCriteria(c => c.IsActive);
        ApplyOrderBy(c => c.SortOrder);
    }
}

public class ColorByIdSpec : Specification<Color>
{
    public ColorByIdSpec(Guid id) => ApplyCriteria(c => c.Id == id);
}

/// <summary>按 code 查找颜色（可选排除自身 Id）。不含 IsActive 过滤——
/// 用于 code 唯一性校验（停用也占用 code）。</summary>
public class ColorByCodeSpec : Specification<Color>
{
    public ColorByCodeSpec(string code, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(c => c.Code == code && (exclude == null || c.Id != exclude.Value));
    }
}
