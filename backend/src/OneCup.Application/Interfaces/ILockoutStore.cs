namespace OneCup.Application.Interfaces;

/// <summary>
/// 失败锁定存储抽象。当前为内存实现,多实例部署替换为 Redis(见 spec 9.2)。
/// </summary>
public interface ILockoutStore
{
    Task<bool> IsLockedAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// 记录一次失败尝试。返回 true 表示本次失败恰好使账号进入锁定状态（达到 MaxFailedAttempts 阈值）。
    /// </summary>
    Task<bool> RecordFailureAsync(string key, CancellationToken ct = default);

    Task ResetAsync(string key, CancellationToken ct = default);
    Task<TimeSpan?> GetRemainingLockoutAsync(string key, CancellationToken ct = default);
}
