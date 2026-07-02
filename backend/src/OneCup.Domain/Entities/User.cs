namespace OneCup.Domain.Entities;

/// <summary>
/// 系统用户。
/// </summary>
public class User : BaseEntity
{
    /// <summary>登录用户名（唯一）</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>BCrypt 哈希后的密码</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>显示名</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>邮箱</summary>
    public string? Email { get; set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>用户的角色集合（多对多）</summary>
    public List<Role> Roles { get; set; } = [];

    /// <summary>用户的刷新令牌集合（一对多）</summary>
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
