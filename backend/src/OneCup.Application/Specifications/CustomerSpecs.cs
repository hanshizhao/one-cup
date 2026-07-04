using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>
/// 仅按 keyword/code/isActive 过滤的客户查询规范（不含分页/排序）。
/// 专用于 CountAsync 统计总数——若用带分页的规范统计，
/// Repository.CountAsync 会应用 Skip/Take，导致只统计当前页子集。
/// 与 <see cref="CustomerPagedSpec"/> 共享相同的 Where 条件。
/// </summary>
/// <remarks>
/// 基类 ApplyCriteria 是覆盖语义，多条件必须组合为单一 predicate 后调用一次。
/// </remarks>
public class CustomerFilterSpec : Specification<Customer>
{
    public CustomerFilterSpec(string? keyword, string? code, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(c =>
            (kw == null || c.Name.Contains(kw) || c.ShortName!.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || c.Code.Contains(code)) &&
            (isActive == null || c.IsActive == isActive.Value));
    }
}

/// <summary>客户分页查询（含过滤、按创建时间倒序）。</summary>
public class CustomerPagedSpec : Specification<Customer>
{
    public CustomerPagedSpec(string? keyword, string? code, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(c =>
            (kw == null || c.Name.Contains(kw) || c.ShortName!.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || c.Code.Contains(code)) &&
            (isActive == null || c.IsActive == isActive.Value));

        ApplyOrderByDescending(c => c.CreatedAt);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>按 Id 查询客户（tracked，详情/更新用 FirstOrDefaultAsync）。</summary>
public class CustomerByIdSpec : Specification<Customer>
{
    public CustomerByIdSpec(Guid id)
    {
        ApplyCriteria(c => c.Id == id);
    }
}

/// <summary>名称唯一性校验（配合 AnyIgnoringFiltersAsync，绕过软删除过滤器）。</summary>
public class CustomerByNameSpec : Specification<Customer>
{
    public CustomerByNameSpec(string name, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(c => c.Name == name && (exclude == null || c.Id != exclude.Value));
    }
}
