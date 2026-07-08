using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>设备过滤规格（仅过滤，不含分页）。用于 CountAsync。</summary>
public class EquipmentFilterSpec : Specification<Equipment>
{
    public EquipmentFilterSpec(string? keyword, string? code, Guid? typeId, bool? isActive, Domain.Enums.EquipmentStatus? status)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(e =>
            (kw == null || e.Code.Contains(kw) || e.Name.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || e.Code.Contains(code)) &&
            (typeId == null || e.EquipmentTypeId == typeId.Value) &&
            (isActive == null || e.IsActive == isActive.Value) &&
            (status == null || e.Status == status.Value));
    }
}

/// <summary>设备分页查询（含过滤，按 SortOrder 升序）。</summary>
public class EquipmentPagedSpec : Specification<Equipment>
{
    public EquipmentPagedSpec(string? keyword, string? code, Guid? typeId, bool? isActive, Domain.Enums.EquipmentStatus? status, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(e =>
            (kw == null || e.Code.Contains(kw) || e.Name.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || e.Code.Contains(code)) &&
            (typeId == null || e.EquipmentTypeId == typeId.Value) &&
            (isActive == null || e.IsActive == isActive.Value) &&
            (status == null || e.Status == status.Value));
        ApplyOrderBy(e => e.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>按 Id 查询设备（tracked）。</summary>
public class EquipmentByIdSpec : Specification<Equipment>
{
    public EquipmentByIdSpec(Guid id) => ApplyCriteria(e => e.Id == id);
}

/// <summary>名称唯一性校验（绕过软删除过滤器）。</summary>
public class EquipmentByNameSpec : Specification<Equipment>
{
    public EquipmentByNameSpec(string name, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(e => e.Name == name && (exclude == null || e.Id != exclude.Value));
    }
}

/// <summary>按设备类型查询（用于删除类型前的引用校验）。</summary>
public class EquipmentByTypeSpec : Specification<Equipment>
{
    public EquipmentByTypeSpec(Guid typeId) => ApplyCriteria(e => e.EquipmentTypeId == typeId);
}
