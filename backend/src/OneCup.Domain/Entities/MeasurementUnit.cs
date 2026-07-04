namespace OneCup.Domain.Entities;

/// <summary>
/// 计量单位字典。code 创建后不可改。
/// 同类单位按 factor 相对基准单位换算（基准 factor=1）。
/// </summary>
public class MeasurementUnit : BaseEntity
{
    /// <summary>英文标识符，如 kg。创建后不可改</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>中文名，如"千克"</summary>
    public string NameZh { get; set; } = string.Empty;

    /// <summary>英文名，如"Kilogram"</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>符号，如 kg / m / tex</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>单位类别，如 LENGTH/WEIGHT/YARN</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>是否为该类别的基准单位（每类有且仅有一个）</summary>
    public bool IsBase { get; set; }

    /// <summary>相对该类别基准单位的换算系数（基准=1）</summary>
    public decimal Factor { get; set; } = 1m;

    /// <summary>展示小数位数（0-6）</summary>
    public int Precision { get; set; } = 2;

    /// <summary>排序号（下拉显示顺序）</summary>
    public int SortOrder { get; set; }

    /// <summary>启停状态（停用后不参与换算、下拉不显示，不物理删除）</summary>
    public bool IsActive { get; set; } = true;
}
