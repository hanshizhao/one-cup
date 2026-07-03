namespace OneCup.Domain.Entities;

/// <summary>
/// 编号分类字典。挂在业务类型下，code 即编号拼码中的分类段。
/// 如面料下：棉(COT)、涤纶(POL)。唯一性：(targetTypeCode, code) 组合唯一。
/// </summary>
public class NumberingCategory : BaseEntity
{
    /// <summary>所属业务类型 code，如 fabric</summary>
    public string TargetTypeCode { get; set; } = string.Empty;

    /// <summary>分类码，如 COT。创建后不可改，即编号里的分类段</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>中文名，如"棉"</summary>
    public string NameZh { get; set; } = string.Empty;

    /// <summary>英文名，如"Cotton"</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>启停状态</summary>
    public bool IsActive { get; set; } = true;
}
