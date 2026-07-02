namespace OneCup.Application.Options;

/// <summary>失败锁定参数。内存方案,多实例需换 Redis(见 spec 9.2)。</summary>
public class LockoutOptions
{
    public const string SectionName = "Lockout";

    /// <summary>连续失败多少次后锁定。默认 5。</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>锁定时长。默认 15 分钟。</summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}
