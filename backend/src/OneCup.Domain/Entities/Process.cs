namespace OneCup.Domain.Entities;

/// <summary>
/// 工序档案。工序编号由编号系统在创建事务内生成。
/// </summary>
public class Process : BaseEntity, ISoftDeletable
{
    /// <summary>工序编号（编号系统生成，如 PRC-0001）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>工序名称（分类内唯一）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>工序分类（前处理/染色/后整理…），可空</summary>
    public string? Category { get; set; }

    /// <summary>排序号（列表按此升序）</summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>启用状态</summary>
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; } = false;
}
