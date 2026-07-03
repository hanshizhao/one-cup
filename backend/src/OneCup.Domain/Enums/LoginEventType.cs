namespace OneCup.Domain.Enums;

/// <summary>
/// 登录日志的事件类型（会话生命周期）。
/// </summary>
public enum LoginEventType
{
    Login,
    Logout,
    Refresh,
    Locked
}
