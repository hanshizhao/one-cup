namespace OneCup.Domain.Exceptions;

/// <summary>账号因连续登录失败被锁定。</summary>
public class AccountLockedException : UnauthorizedException
{
    /// <summary>剩余锁定时长。</summary>
    public TimeSpan? RetryAfter { get; }

    public AccountLockedException(TimeSpan? retryAfter)
        : base("账号已被锁定,请稍后再试")
    {
        RetryAfter = retryAfter;
    }
}
