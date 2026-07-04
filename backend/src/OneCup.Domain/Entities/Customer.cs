namespace OneCup.Domain.Entities;

/// <summary>
/// 客户档案。客户编号由编号系统在创建事务内生成。
/// </summary>
public class Customer : BaseEntity, ISoftDeletable
{
    /// <summary>客户编号（编号系统生成，如 CUST-0001）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>客户名称（全名，唯一）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>客户简称（可重复）</summary>
    public string? ShortName { get; set; }

    /// <summary>联系人</summary>
    public string? ContactPerson { get; set; }

    /// <summary>联系电话</summary>
    public string? ContactPhone { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>启用状态（停用的客户不再用于新业务，但保留历史）</summary>
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; } = false;
}
