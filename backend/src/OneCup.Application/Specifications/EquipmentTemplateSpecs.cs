using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>模板过滤规格（仅过滤，不含分页）。用于 CountAsync。</summary>
public class EquipmentTemplateFilterSpec : Specification<EquipmentTemplate>
{
    public EquipmentTemplateFilterSpec(Guid? typeId, string? keyword, Guid? processId)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(t =>
            (typeId == null || t.EquipmentTypeId == typeId.Value) &&
            (kw == null || t.Name.Contains(kw)) &&
            (processId == null || t.ProcessId == processId.Value));
    }
}

/// <summary>模板跨类型分页查询（含过滤，按 SortOrder 升序）。含 Values 以便读时算 status。</summary>
public class EquipmentTemplatePagedSpec : Specification<EquipmentTemplate>
{
    public EquipmentTemplatePagedSpec(Guid? typeId, string? keyword, Guid? processId, int page, int pageSize)
    {
        ApplyInclude(nameof(EquipmentTemplate.Values));
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(t =>
            (typeId == null || t.EquipmentTypeId == typeId.Value) &&
            (kw == null || t.Name.Contains(kw)) &&
            (processId == null || t.ProcessId == processId.Value));
        ApplyOrderBy(t => t.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>按 Id 查询模板（含 Values 以便读时算 status）。</summary>
public class EquipmentTemplateByIdSpec : Specification<EquipmentTemplate>
{
    public EquipmentTemplateByIdSpec(Guid id)
    {
        ApplyCriteria(t => t.Id == id);
        ApplyInclude(nameof(EquipmentTemplate.Values));
    }
}
