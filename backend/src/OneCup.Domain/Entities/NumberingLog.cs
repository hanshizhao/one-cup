namespace OneCup.Domain.Entities;

/// <summary>
/// 编码生成日志。每次取号一条，只追加不修改。
/// 不关联业务对象 ID（保持引擎与业务解耦）。
/// </summary>
public class NumberingLog : BaseEntity
{
    /// <summary>生成的完整编码</summary>
    public string GeneratedCode { get; set; } = string.Empty;

    public Guid RuleId { get; set; }

    /// <summary>业务对象类型（冗余存，便于不 join 规则表直接筛）</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>品类码（可空）</summary>
    public string? CategoryCode { get; set; }

    /// <summary>周期键（可空）</summary>
    public string? PeriodKey { get; set; }

    /// <summary>流水号数值</summary>
    public int SeqValue { get; set; }

    public NumberingRule? Rule { get; set; }
}
