namespace OneCup.Domain.Entities;

/// <summary>
/// 物料(染化料/助剂/原材料)。坯布面料生产过程中的投入品。
/// code 创建后不可改,作为业务模块的稳定引用标识符。
/// </summary>
public class Material : BaseEntity
{
    /// <summary>编码,如 DYE-0001。创建后不可改</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>名称,如"活性红 3B"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>规格型号,如"粉末 100%"</summary>
    public string Spec { get; set; } = string.Empty;

    /// <summary>原料类别(自由文本),如"助剂/染料/原材料"</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>计量单位 Id,关联 measurement_units;可空以兼容"暂未定单位"</summary>
    public Guid? UnitId { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>启停状态(停用后引用方按需处理,可物理删除)</summary>
    public bool IsActive { get; set; } = true;
}
