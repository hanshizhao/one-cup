namespace OneCup.Application.Interfaces;

/// <summary>
/// 编号时间提供者。GetCurrentTime 返回用于周期键/日期段计算的北京时间。
/// 数据库时间戳仍用 UTC（不经过此接口）。
/// </summary>
public interface INumberingClock
{
    DateTime GetCurrentTime();
}
