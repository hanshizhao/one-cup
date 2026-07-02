namespace OneCup.Domain.Entities;

/// <summary>
/// 编号计数器。一个「规则+品类+周期」对应一行（一个"桶"）。
/// category_code/period_key 用空串代替 NULL（避免 PG 唯一索引 NULL 歧义）。
/// </summary>
public class NumberingCounter : BaseEntity
{
    public Guid RuleId { get; set; }

    /// <summary>品类码（无品类时为空串）</summary>
    public string CategoryCode { get; set; } = string.Empty;

    /// <summary>周期键（不重置时为空串；按年="2026"；按月="202607"；按日="20260702"）</summary>
    public string PeriodKey { get; set; } = string.Empty;

    /// <summary>当前已分配到的最大流水号</summary>
    public int CurrentSeq { get; set; }

    public NumberingRule? Rule { get; set; }
}
