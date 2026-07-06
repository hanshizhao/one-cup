using OneCup.Domain.Enums;

namespace OneCup.Domain.Entities;

/// <summary>
/// 设备类型的参数定义。描述该类型设备有哪些参数、每个参数的约束。
/// 随 EquipmentType 整表替换（PUT 时按 Id diff：null=新增、有值=更新、缺失=删除）。
/// 物理删除——删除后引用它的模板值变孤儿，由读时校验检测。
/// </summary>
public class EquipmentTypeParameter : BaseEntity
{
    public Guid EquipmentTypeId { get; set; }

    /// <summary>参数名（同类型内唯一）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>值类型</summary>
    public ParameterValueType ValueType { get; set; }

    /// <summary>单位（关联计量单位，Number 类型用）</summary>
    public Guid? UnitId { get; set; }

    /// <summary>数值下限（Number 类型）</summary>
    public string? MinValue { get; set; }

    /// <summary>数值上限（Number 类型）</summary>
    public string? MaxValue { get; set; }

    /// <summary>小数位限制（Number 类型）</summary>
    public int? Precision { get; set; }

    /// <summary>枚举可选值（JSON 数组字符串，Enum 类型）</summary>
    public string? Options { get; set; }

    public bool Required { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public string? Remark { get; set; }
}
