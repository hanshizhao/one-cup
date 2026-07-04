using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>
/// 仅按 keyword/category/isActive 过滤的工序查询规范（不含分页/排序）。
/// 专用于 CountAsync 统计总数——若用带分页的规范统计，
/// Repository.CountAsync 会应用 Skip/Take，导致只统计当前页子集。
/// 与 <see cref="ProcessPagedSpec"/> 共享相同的 Where 条件。
/// </summary>
/// <remarks>
/// 基类 ApplyCriteria 是覆盖语义，多条件必须组合为单一 predicate 后调用一次。
/// </remarks>
public class ProcessFilterSpec : Specification<Process>
{
    public ProcessFilterSpec(string? keyword, string? category, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        ApplyCriteria(p =>
            (kw == null || p.Name.Contains(kw) || p.Code.Contains(kw)) &&
            // category 精确匹配：传入非空则等值，传入空则不限（含 NULL 行）
            (cat == null || p.Category == cat) &&
            (isActive == null || p.IsActive == isActive.Value));
    }
}

/// <summary>工序分页查询（含过滤、按 SortOrder 升序单字段）。</summary>
/// <remarks>Specification 基类只支持单字段 OrderBy，无 ThenBy；SortOrder 相同时次序不稳定。</remarks>
public class ProcessPagedSpec : Specification<Process>
{
    public ProcessPagedSpec(string? keyword, string? category, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        ApplyCriteria(p =>
            (kw == null || p.Name.Contains(kw) || p.Code.Contains(kw)) &&
            (cat == null || p.Category == cat) &&
            (isActive == null || p.IsActive == isActive.Value));

        ApplyOrderBy(p => p.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>按 Id 查询工序（tracked，详情/更新用 FirstOrDefaultAsync）。</summary>
public class ProcessByIdSpec : Specification<Process>
{
    public ProcessByIdSpec(Guid id)
    {
        ApplyCriteria(p => p.Id == id);
    }
}

/// <summary>
/// 名称「分类内唯一」校验（配合 AnyIgnoringFiltersAsync，绕过软删除过滤器）。
/// 关键：Category 为 null 时显式匹配 p.Category == null，不依赖 DB 唯一索引对 NULL 的处理。
/// </summary>
public class ProcessByNameSpec : Specification<Process>
{
    public ProcessByNameSpec(string name, string? category, Guid? excludingId = null)
    {
        var cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        var exclude = excludingId;
        ApplyCriteria(p =>
            p.Name == name &&
            (cat == null ? p.Category == null : p.Category == cat) &&
            (exclude == null || p.Id != exclude.Value));
    }
}
