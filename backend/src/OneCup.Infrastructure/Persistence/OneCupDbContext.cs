using Microsoft.EntityFrameworkCore;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence;

/// <summary>
/// EF Core 数据库上下文。
/// </summary>
public class OneCupDbContext : DbContext
{
    public OneCupDbContext(DbContextOptions<OneCupDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 应用程序集内所有 IEntityTypeConfiguration 配置
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OneCupDbContext).Assembly);
    }
}
