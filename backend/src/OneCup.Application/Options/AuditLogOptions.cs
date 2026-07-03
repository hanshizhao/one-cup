namespace OneCup.Application.Options;

/// <summary>
/// 审计日志相关配置，绑定 appsettings 的 AuditLog 节。
/// </summary>
public class AuditLogOptions
{
    public const string SectionName = "AuditLog";

    /// <summary>日志保留天数（超期由清理任务删除）。默认 180 天。</summary>
    public int RetentionDays { get; set; } = 180;

    /// <summary>每日清理执行时刻（本地时间 "HH:mm"）。默认 "03:00"。</summary>
    public string CleanupTime { get; set; } = "03:00";

    /// <summary>写入队列容量（有界 Channel 容量，满则 DropOldest）。默认 10000。</summary>
    public int QueueCapacity { get; set; } = 10000;

    /// <summary>单批写入条数。默认 100。</summary>
    public int BatchSize { get; set; } = 100;
}
