namespace OneCup.Application.Common;

/// <summary>
/// 已知业务对象类型清单（初始值）。
/// 【已降级】字典化改造后，业务类型由 numbering_target_types 表管理，引擎通过强校验消费字典。
/// 本常量类仅保留供种子迁移引用初始 6 个类型，业务代码不应硬编码引用，改用字典查询。
/// </summary>
public static class NumberTargetTypes
{
    public const string Fabric = "fabric";
    public const string Material = "material";
    public const string Equipment = "equipment";
    public const string Customer = "customer";
    public const string Color = "color";
    public const string Product = "product";
    public const string Process = "process";
    public const string EquipmentType = "equipment-type";
}
