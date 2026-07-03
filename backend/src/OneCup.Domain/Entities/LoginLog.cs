using OneCup.Domain.Enums;

namespace OneCup.Domain.Entities;

/// <summary>
/// 登录日志：记录会话生命周期事件（登录/登出/刷新/锁定）。
/// 只追加，不可修改。UpdatedAt 永远为 null。
/// </summary>
public class LoginLog : BaseEntity
{
    /// <summary>用户 Id；登录失败且账号不存在时为 null。</summary>
    public Guid? UserId { get; set; }

    /// <summary>尝试登录的账号（即使不存在也记）。</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>事件类型。</summary>
    public LoginEventType EventType { get; set; }

    /// <summary>结果：Success/Failed。</summary>
    public OperationResult Result { get; set; }

    /// <summary>客户端 IP。</summary>
    public string? IpAddress { get; set; }

    /// <summary>客户端 User-Agent（截断）。</summary>
    public string? UserAgent { get; set; }

    /// <summary>失败原因分类：InvalidCredentials/AccountLocked/UserNotFound/InvalidRefreshToken 等。</summary>
    public string? FailureReason { get; set; }

    /// <summary>人类可读补充信息。</summary>
    public string? Message { get; set; }
}
