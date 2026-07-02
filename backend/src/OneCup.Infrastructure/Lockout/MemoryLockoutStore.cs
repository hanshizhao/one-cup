using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OneCup.Application.Options;
using OneCup.Infrastructure.Interfaces;

namespace OneCup.Infrastructure.Lockout;

/// <summary>
/// 基于 IMemoryCache 的失败锁定存储。单实例可用。
/// 计数 key:lockout:fail:{key}; 锁定 key:lockout:until:{key}。
/// </summary>
public class MemoryLockoutStore : ILockoutStore
{
    private readonly IMemoryCache _cache;
    private readonly LockoutOptions _options;

    public MemoryLockoutStore(IMemoryCache cache, IOptions<LockoutOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public Task<bool> IsLockedAsync(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue<DateTimeOffset>(LockKey(key), out var until))
        {
            return Task.FromResult(until > DateTimeOffset.UtcNow);
        }
        return Task.FromResult(false);
    }

    public Task RecordFailureAsync(string key, CancellationToken ct = default)
    {
        var failKey = FailKey(key);
        var attempts = _cache.GetOrCreate(failKey, e =>
        {
            e.AbsoluteExpirationRelativeToNow = _options.LockoutDuration;
            return 0;
        }) + 1;

        if (attempts >= _options.MaxFailedAttempts)
        {
            var lockedUntil = DateTimeOffset.UtcNow.Add(_options.LockoutDuration);
            _cache.Set(LockKey(key), lockedUntil, _options.LockoutDuration);
        }
        else
        {
            _cache.Set(failKey, attempts, _options.LockoutDuration);
        }
        return Task.CompletedTask;
    }

    public Task ResetAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(FailKey(key));
        _cache.Remove(LockKey(key));
        return Task.CompletedTask;
    }

    public Task<TimeSpan?> GetRemainingLockoutAsync(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue<DateTimeOffset>(LockKey(key), out var until))
        {
            var remaining = until - DateTimeOffset.UtcNow;
            return Task.FromResult<TimeSpan?>(remaining > TimeSpan.Zero ? remaining : null);
        }
        return Task.FromResult<TimeSpan?>(null);
    }

    private static string FailKey(string key) => $"lockout:fail:{key}";
    private static string LockKey(string key) => $"lockout:until:{key}";
}
