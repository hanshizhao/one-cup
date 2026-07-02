using OneCup.Domain.Enums;

namespace OneCup.Domain.Entities;

/// <summary>
/// 编码规则。一条规则描述某个业务对象类型的编码如何生成。
/// </summary>
public class NumberingRule : BaseEntity
{
    /// <summary>业务对象类型（字符串，见 NumberTargetTypes，引擎不校验合法性）</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>规则名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>固定前缀段，如 FAB</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>是否拼分类码段</summary>
    public bool IncludeCategory { get; set; }

    /// <summary>日期段类型</summary>
    public DateSegment DateSegment { get; set; } = DateSegment.None;

    /// <summary>流水号位数（补零），1–8</summary>
    public short SeqLength { get; set; } = 4;

    /// <summary>段间分隔符，默认 "-"，可空串</summary>
    public string Separator { get; set; } = "-";

    /// <summary>重置周期</summary>
    public ResetPeriod ResetPeriod { get; set; } = ResetPeriod.None;

    /// <summary>启停状态（停用替代物理删除）</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
