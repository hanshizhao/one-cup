namespace OneCup.Domain.Entities;

/// <summary>
/// 权限，按"资源:动作"编码（如 fabric:read）。
/// </summary>
public class Permission : BaseEntity
{
    /// <summary>权限编码，格式 资源:动作（如 fabric:read）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>显示名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>描述</summary>
    public string? Description { get; set; }

    /// <summary>拥有此权限的角色集合（多对多）</summary>
    public List<Role> Roles { get; set; } = [];
}
