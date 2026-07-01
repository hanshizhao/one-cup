using Microsoft.EntityFrameworkCore;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence;

/// <summary>
/// EF Core 数据库上下文。
/// 各业务模块的 DbSet 将在后续按模块详细设计时补充。
/// 当前为骨架状态,仅包含基类配置。
/// </summary>
public class OneCupDbContext : DbContext
{
    public OneCupDbContext(DbContextOptions<OneCupDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 统一约定:所有实体表名使用 snake_case (PostgreSQL 惯例)
        // 各模块的实体配置在后续补充
        // modelBuilder.ApplyConfigurationsFromAssembly(typeof(OneCupDbContext).Assembly);
    }
}
