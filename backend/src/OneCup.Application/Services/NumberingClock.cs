using OneCup.Application.Interfaces;

namespace OneCup.Application.Services;

/// <summary>
/// 编号时间提供者实现：返回北京时间（UTC+8）。
/// 时区 ID 统一用 IANA "Asia/Shanghai"，.NET 10 在 Windows/Linux 全平台通用。
/// </summary>
public class NumberingClock : INumberingClock
{
    private static readonly TimeZoneInfo ChinaTz =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

    public DateTime GetCurrentTime() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ChinaTz);
}
