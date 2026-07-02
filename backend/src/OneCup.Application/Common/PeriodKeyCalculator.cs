using OneCup.Domain.Enums;

namespace OneCup.Application.Common;

/// <summary>
/// 周期键计算（纯函数）。传入的 now 应已是目标时区（北京时间）的时间。
/// </summary>
public static class PeriodKeyCalculator
{
    public static string Calc(ResetPeriod resetPeriod, DateTime now) => resetPeriod switch
    {
        ResetPeriod.None => "",
        ResetPeriod.Yearly => now.Year.ToString(),
        ResetPeriod.Monthly => now.ToString("yyyyMM"),
        ResetPeriod.Daily => now.ToString("yyyyMMdd"),
        _ => ""
    };
}
