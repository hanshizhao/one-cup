namespace OneCup.Domain.Entities;

/// <summary>
/// 颜色主数据字典。code 创建后不可改，作为面料/产品等业务模块的稳定引用标识符。
/// </summary>
public class Color : BaseEntity
{
    /// <summary>编码，如 RED001。创建后不可改</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>中文名，如"大红"</summary>
    public string NameZh { get; set; } = string.Empty;

    /// <summary>英文名，如"Red"</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>颜色值 #RRGGBB</summary>
    public string Hex { get; set; } = string.Empty;

    /// <summary>颜色系（自由文本，如"红"）</summary>
    public string ColorFamily { get; set; } = string.Empty;

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>启停状态（停用后引用方按需处理，不物理删除）</summary>
    public bool IsActive { get; set; } = true;
}
