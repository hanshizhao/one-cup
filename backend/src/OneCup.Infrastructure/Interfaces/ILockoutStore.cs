namespace OneCup.Infrastructure.Interfaces;

/// <summary>
/// 失败锁定存储抽象。当前为内存实现,多实例部署替换为 Redis(见 spec 9.2)。
/// </summary>
public interface ILockoutStore
{
    Task<bool> IsLockedAsync(string key, CancellationToken ct = default);
    Task RecordFailureAsync(string key, CancellationToken ct = default);
    Task ResetAsync(string key, CancellationToken ct = default);
    Task<TimeSpan?> GetRemainingLockoutAsync(string key, CancellationToken ct = default);
}
