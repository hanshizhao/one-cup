namespace OneCup.Domain.Entities;

/// <summary>
/// 模板的参数值。Value 用统一字符串列承载所有类型（Number/Text/Enum）。
/// (TemplateId, ParameterId) 唯一——一个模板对同一参数只有一个值。
/// </summary>
public class EquipmentTemplateValue : BaseEntity
{
    public Guid EquipmentTemplateId { get; set; }

    /// <summary>引用参数定义（EquipmentTypeParameter）</summary>
    public Guid ParameterId { get; set; }

    /// <summary>统一字符串承载（Number/Text/Enum 共用）</summary>
    public string? Value { get; set; }
}
