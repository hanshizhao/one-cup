namespace OneCup.Domain.Entities;

/// <summary>
/// 角色，聚合多个权限。
/// </summary>
public class Role : BaseEntity
{
    /// <summary>显示名（如"管理员"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>角色编码（如 admin）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>描述</summary>
    public string? Description { get; set; }

    /// <summary>此角色下的用户集合（多对多）</summary>
    public List<User> Users { get; set; } = [];

    /// <summary>此角色拥有的权限集合（多对多）</summary>
    public List<Permission> Permissions { get; set; } = [];
}
