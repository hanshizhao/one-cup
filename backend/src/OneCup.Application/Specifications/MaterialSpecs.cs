using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>物料过滤规格(仅 keyword/category/isActive,不含分页)。用于 CountAsync 统计总数。</summary>
/// <remarks>多条件组合为单一 predicate 调一次 ApplyCriteria(基类覆盖语义)。</remarks>
public class MaterialFilterSpec : Specification<Material>
{
    public MaterialFilterSpec(string? keyword, string? category, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        ApplyCriteria(m =>
            (kw == null || m.Code.Contains(kw) || m.Name.Contains(kw) || m.Spec.Contains(kw)) &&
            (cat == null || m.Category == cat) &&
            (isActive == null || m.IsActive == isActive.Value));
    }
}

/// <summary>物料分页查询(含 keyword/category/isActive 过滤,按 SortOrder 升序)。</summary>
public class MaterialPagedSpec : Specification<Material>
{
    public MaterialPagedSpec(string? keyword, string? category, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        ApplyCriteria(m =>
            (kw == null || m.Code.Contains(kw) || m.Name.Contains(kw) || m.Spec.Contains(kw)) &&
            (cat == null || m.Category == cat) &&
            (isActive == null || m.IsActive == isActive.Value));
        ApplyOrderBy(m => m.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>物料全部启用项(前端下拉用,按 SortOrder 升序)。</summary>
public class MaterialActiveSpec : Specification<Material>
{
    public MaterialActiveSpec()
    {
        ApplyCriteria(m => m.IsActive);
        ApplyOrderBy(m => m.SortOrder);
    }
}

public class MaterialByIdSpec : Specification<Material>
{
    public MaterialByIdSpec(Guid id) => ApplyCriteria(m => m.Id == id);
}

/// <summary>按 code 查找物料(可选排除自身 Id)。不含 IsActive 过滤——用于 code 唯一性校验。</summary>
public class MaterialByCodeSpec : Specification<Material>
{
    public MaterialByCodeSpec(string code, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(m => m.Code == code && (exclude == null || m.Id != exclude.Value));
    }
}
