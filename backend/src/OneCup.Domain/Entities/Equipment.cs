using OneCup.Domain.Enums;

namespace OneCup.Domain.Entities;

/// <summary>
/// 设备实例。编号由编号系统生成。软删除（未来被工单引用需保留审计）。
/// 不存参数值——参数值只存在于运行模板。
/// </summary>
public class Equipment : BaseEntity, ISoftDeletable
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>所属设备类型（FK，无导航属性，与 Material→Unit 一致的宽松耦合）</summary>
    public Guid EquipmentTypeId { get; set; }

    /// <summary>规格型号</summary>
    public string? Specification { get; set; }

    public string? Supplier { get; set; }
    public string? Location { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Running;
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public bool IsDeleted { get; set; } = false;
}
