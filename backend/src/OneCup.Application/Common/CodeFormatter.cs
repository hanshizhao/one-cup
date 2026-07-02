using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Common;

/// <summary>
/// 编码拼码（纯函数）。段顺序固定：[前缀] [分类码?] [日期?] [流水号]，用 separator 连接。
/// now 应已是目标时区（北京时间）的时间。
/// </summary>
public static class CodeFormatter
{
    /// <summary>
    /// 拼出实际编码。
    /// </summary>
    public static string Format(
        string prefix, bool includeCategory, DateSegment dateSegment,
        int seqLength, string separator, int seq, string? categoryCode, DateTime now)
    {
        var segments = new List<string> { prefix };

        if (includeCategory && !string.IsNullOrEmpty(categoryCode))
            segments.Add(categoryCode);

        var datePart = dateSegment switch
        {
            DateSegment.None => null,
            DateSegment.Year => now.ToString("yyyy"),
            DateSegment.YearMonth => now.ToString("yyyyMM"),
            DateSegment.YearMonthDay => now.ToString("yyyyMMdd"),
            _ => null
        };
        if (datePart is not null) segments.Add(datePart);

        // 流水号溢出校验：实际位数超过配置位数 → 阻断（设计 6.5，不自动扩位）
        if (seq.ToString().Length > seqLength)
            throw new DomainException($"流水号已超出配置位数 {seqLength}，请调整规则或联系管理员");

        segments.Add(seq.ToString(new string('0', seqLength)));

        return string.Join(separator, segments);
    }

    /// <summary>
    /// 拼出展示用的示例编码（列表/详情 sampleFormat 字段）。
    /// 用占位品类码 "CAT"、seq=1。
    /// </summary>
    public static string FormatSample(
        string prefix, bool includeCategory, DateSegment dateSegment,
        int seqLength, string separator, DateTime now)
    {
        return Format(prefix, includeCategory, dateSegment, seqLength, separator,
            1, includeCategory ? "CAT" : null, now);
    }
}
