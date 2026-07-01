namespace OneCup.Domain.Entities;

/// <summary>
/// 所有实体的基类,提供公共审计字段。
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
