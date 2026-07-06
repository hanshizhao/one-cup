namespace OneCup.Domain.Entities;

/// <summary>
/// 运行模板——某设备类型在某工序下的运行方案（如烧毛机·烧毛工序·轻烧）。
/// 绑定 (EquipmentTypeId, ProcessId)，名称在三元组内唯一。
/// 不走编号引擎。物理删除。
/// </summary>
public class EquipmentTemplate : BaseEntity
{
    public Guid EquipmentTypeId { get; set; }
    public Guid ProcessId { get; set; }

    /// <summary>模板名（TypeId+ProcessId+Name 唯一）</summary>
    public string Name { get; set; } = string.Empty;

    public string? Remark { get; set; }
    public int SortOrder { get; set; } = 0;

    /// <summary>参数值集合</summary>
    public List<EquipmentTemplateValue> Values { get; set; } = new();
}
