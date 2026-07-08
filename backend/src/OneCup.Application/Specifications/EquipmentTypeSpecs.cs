using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>设备类型过滤规格（仅过滤，不含分页）。用于 CountAsync。</summary>
public class EquipmentTypeFilterSpec : Specification<EquipmentType>
{
    public EquipmentTypeFilterSpec(string? keyword, string? code, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(e =>
            (kw == null || e.Code.Contains(kw) || e.Name.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || e.Code.Contains(code)) &&
            (isActive == null || e.IsActive == isActive.Value));
    }
}

/// <summary>设备类型分页查询（含过滤，按 SortOrder 升序）。</summary>
public class EquipmentTypePagedSpec : Specification<EquipmentType>
{
    public EquipmentTypePagedSpec(string? keyword, string? code, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(e =>
            (kw == null || e.Code.Contains(kw) || e.Name.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || e.Code.Contains(code)) &&
            (isActive == null || e.IsActive == isActive.Value));
        ApplyOrderBy(e => e.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>设备类型全部启用项（前端下拉用，按 SortOrder 升序）。</summary>
public class EquipmentTypeActiveSpec : Specification<EquipmentType>
{
    public EquipmentTypeActiveSpec()
    {
        ApplyCriteria(e => e.IsActive);
        ApplyOrderBy(e => e.SortOrder);
    }
}

/// <summary>按 Id 查询设备类型（tracked）。</summary>
public class EquipmentTypeByIdSpec : Specification<EquipmentType>
{
    public EquipmentTypeByIdSpec(Guid id)
    {
        ApplyCriteria(e => e.Id == id);
        ApplyInclude(nameof(EquipmentType.Parameters));
        ApplyInclude(nameof(EquipmentType.Templates));
        ApplyInclude(nameof(EquipmentType.Templates) + ".Values");
    }
}

/// <summary>按多个 Id 批量查询设备类型（含参数定义，用于跨类型模板查询算 status）。</summary>
public class EquipmentTypesByIdsSpec : Specification<EquipmentType>
{
    public EquipmentTypesByIdsSpec(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        ApplyCriteria(t => idList.Contains(t.Id));
        ApplyInclude(nameof(EquipmentType.Parameters));
    }
}

/// <summary>名称唯一性校验。</summary>
public class EquipmentTypeByNameSpec : Specification<EquipmentType>
{
    public EquipmentTypeByNameSpec(string name, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(e => e.Name == name && (exclude == null || e.Id != exclude.Value));
    }
}
