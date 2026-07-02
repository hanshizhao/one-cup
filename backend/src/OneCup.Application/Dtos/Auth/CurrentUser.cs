namespace OneCup.Application.Dtos.Auth;

/// <summary>
/// 当前登录用户信息（GET /api/auth/me 返回）。
/// </summary>
public class CurrentUser
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>角色编码集合</summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>权限编码集合（admin 为 ["*"]）</summary>
    public List<string> Permissions { get; set; } = [];
}
