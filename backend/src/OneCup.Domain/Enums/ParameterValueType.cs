namespace OneCup.Domain.Enums;

/// <summary>
/// 设备参数的值类型，决定输入控件与校验分支。
/// </summary>
public enum ParameterValueType
{
    /// <summary>数值型 — 带 Min/Max/Precision 范围校验</summary>
    Number = 0,
    /// <summary>文本型 — 自由文本</summary>
    Text = 1,
    /// <summary>枚举型 — 值必须在 Options 列表内</summary>
    Enum = 2,
}
