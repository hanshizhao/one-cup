namespace OneCup.Domain.Entities;

/// <summary>
/// 刷新令牌（opaque，存数据库，支持吊销）。
/// </summary>
public class RefreshToken : BaseEntity
{
    /// <summary>令牌字符串（随机 opaque）</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>归属用户 Id</summary>
    public Guid UserId { get; set; }

    /// <summary>归属用户（导航属性）</summary>
    public User User { get; set; } = null!;

    /// <summary>过期时间</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>是否已吊销</summary>
    public bool IsRevoked { get; set; } = false;
}
