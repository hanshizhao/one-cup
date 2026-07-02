using OneCup.Domain.Enums;

namespace OneCup.Application.Dtos.System;

// ── 规则 ──

public record CreateNumberingRuleRequest
{
    public string TargetType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Prefix { get; init; } = string.Empty;
    public bool IncludeCategory { get; init; }
    public DateSegment DateSegment { get; init; }
    public short SeqLength { get; init; } = 4;
    public string Separator { get; init; } = "-";
    public ResetPeriod ResetPeriod { get; init; }
    public string? Remark { get; init; }
}

public record UpdateNumberingRuleRequest
{
    public string? Name { get; init; }
    public string? Prefix { get; init; }
    public string? TargetType { get; init; }
    public bool? IncludeCategory { get; init; }
    public DateSegment? DateSegment { get; init; }
    public short? SeqLength { get; init; }
    public string? Separator { get; init; }
    public ResetPeriod? ResetPeriod { get; init; }
    public string? Remark { get; init; }
}

public record UpdateRuleStatusRequest
{
    public bool IsActive { get; init; }
}

public class NumberingRuleDto
{
    public Guid Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public bool IncludeCategory { get; set; }
    public DateSegment DateSegment { get; set; }
    public short SeqLength { get; set; }
    public string Separator { get; set; } = "-";
    public ResetPeriod ResetPeriod { get; set; }
    public bool IsActive { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    /// <summary>展示用示例编码，如 FAB-CAT-2026-0001</summary>
    public string SampleFormat { get; set; } = string.Empty;
}

public class NumberingRuleListItemDto
{
    public Guid Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string SampleFormat { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── 预览 ──

public record PreviewCodeResult
{
    public string? Code { get; init; }
    public string Note { get; init; } = "预览编号，实际保存时以系统分配为准";
}

// ── 日志 ──

public class NumberingLogListItemDto
{
    public Guid Id { get; set; }
    public string GeneratedCode { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string? CategoryCode { get; set; }
    public string? PeriodKey { get; set; }
    public int SeqValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RuleName { get; set; }
}
