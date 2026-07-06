namespace OneCup.Domain.Entities;

/// <summary>
/// 设备类型（定型机/染色机/烧毛机…）。编号由编号系统生成。
/// 承载参数定义 schema（EquipmentTypeParameter）和运行模板（EquipmentTemplate）。
/// 物理删除——删除前需校验无设备引用、无模板。
/// </summary>
public class EquipmentType : BaseEntity
{
    /// <summary>类型编号（编号系统生成）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>类型名称（唯一）</summary>
    public string Name { get; set; } = string.Empty;

    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    /// <summary>参数定义 schema（子集合）</summary>
    public List<EquipmentTypeParameter> Parameters { get; set; } = new();

    /// <summary>运行模板</summary>
    public List<EquipmentTemplate> Templates { get; set; } = new();
}
