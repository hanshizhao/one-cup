using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OneCup.Application.Options;
using OneCup.Infrastructure.Lockout;

namespace OneCup.UnitTests.Lockout;

public class MemoryLockoutStoreTests
{
    private static MemoryLockoutStore CreateStore(int maxAttempts = 5, int lockMin = 15)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new LockoutOptions
        {
            MaxFailedAttempts = maxAttempts,
            LockoutDuration = TimeSpan.FromMinutes(lockMin),
        });
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new MemoryLockoutStore(cache, options);
    }

    [Fact]
    public async Task Not_locked_when_under_threshold()
    {
        var store = CreateStore(maxAttempts: 3);
        await store.RecordFailureAsync("u1", default);
        await store.RecordFailureAsync("u1", default);
        Assert.False(await store.IsLockedAsync("u1", default));
    }

    [Fact]
    public async Task Locked_after_threshold_failures()
    {
        var store = CreateStore(maxAttempts: 3);
        for (var i = 0; i < 3; i++) await store.RecordFailureAsync("u1", default);
        Assert.True(await store.IsLockedAsync("u1", default));
    }

    [Fact]
    public async Task Reset_clears_failures()
    {
        var store = CreateStore(maxAttempts: 3);
        for (var i = 0; i < 3; i++) await store.RecordFailureAsync("u1", default);
        await store.ResetAsync("u1", default);
        Assert.False(await store.IsLockedAsync("u1", default));
    }

    [Fact]
    public async Task Remaining_lockout_positive_when_locked()
    {
        var store = CreateStore(maxAttempts: 1, lockMin: 15);
        await store.RecordFailureAsync("u1", default);
        var remaining = await store.GetRemainingLockoutAsync("u1", default);
        Assert.NotNull(remaining);
        Assert.True(remaining > TimeSpan.Zero && remaining <= TimeSpan.FromMinutes(15));
    }
}
