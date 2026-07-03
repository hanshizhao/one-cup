namespace OneCup.Domain.Entities;

/// <summary>
/// 编号业务类型字典。描述可被编号引擎消费的业务对象类型（如面料/原料）。
/// code 创建后不可改，作为编号规则的 target_type 标识。
/// </summary>
public class NumberingTargetType : BaseEntity
{
    /// <summary>英文标识符，如 fabric。创建后不可改</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>中文名，如"面料"</summary>
    public string NameZh { get; set; } = string.Empty;

    /// <summary>英文名，如"Fabric"</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>排序号（下拉显示顺序）</summary>
    public int SortOrder { get; set; }

    /// <summary>启停状态（停用后引擎校验拒绝、下拉不显示，不物理删除）</summary>
    public bool IsActive { get; set; } = true;
}
