namespace OneCup.Application.Common;

/// <summary>
/// 已知业务对象类型清单。引擎不校验 target_type 合法性（见设计 6.1），
/// 此常量类仅作拼写提示与前端下拉选项来源，不强制。
/// </summary>
public static class NumberTargetTypes
{
    public const string Fabric = "fabric";
    public const string Material = "material";
    public const string Equipment = "equipment";
    public const string Customer = "customer";
    public const string Color = "color";
    public const string Product = "product";
}
