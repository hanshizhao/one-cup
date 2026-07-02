namespace OneCup.Domain.Entities;

/// <summary>
/// 所有实体的基类,提供公共审计字段。
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // 不在属性初始化器里用 DateTime.UtcNow — 那会让 EF Core HasData 种子数据
    // 每次构建模型时拿到不同的时间戳，触发 PendingModelChangesWarning。
    // 时间戳由 EF Core 的值生成器在 SaveChanges 时填入（见 OneCupDbContext 配置）。
    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
