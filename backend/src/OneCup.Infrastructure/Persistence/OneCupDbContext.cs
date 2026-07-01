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

        // 种子数据（必须在 ApplyConfigurationsFromAssembly 之后，复用已配置的实体与关系）
        Seed(modelBuilder);
    }

    /// <summary>
    /// 种子数据：1 admin 账号、2 角色、13 权限及其关联。
    /// Guid 均为确定性常量（HasData 要求）。
    /// </summary>
    private void Seed(ModelBuilder modelBuilder)
    {
        // ── 权限 ──
        modelBuilder.Entity<Permission>().HasData(
            new Permission { Id = SeedData.PermFabricRead, Code = "fabric:read", Name = "查看面料开发" },
            new Permission { Id = SeedData.PermFabricWrite, Code = "fabric:write", Name = "录入/编辑面料开发" },
            new Permission { Id = SeedData.PermMaterialRead, Code = "material:read", Name = "查看原料物料" },
            new Permission { Id = SeedData.PermMaterialWrite, Code = "material:write", Name = "维护原料物料" },
            new Permission { Id = SeedData.PermEquipmentRead, Code = "equipment:read", Name = "查看设备" },
            new Permission { Id = SeedData.PermEquipmentWrite, Code = "equipment:write", Name = "维护设备" },
            new Permission { Id = SeedData.PermCustomerRead, Code = "customer:read", Name = "查看客户" },
            new Permission { Id = SeedData.PermCustomerWrite, Code = "customer:write", Name = "维护客户" },
            new Permission { Id = SeedData.PermColorRead, Code = "color:read", Name = "查看颜色对色" },
            new Permission { Id = SeedData.PermColorWrite, Code = "color:write", Name = "维护颜色对色" },
            new Permission { Id = SeedData.PermProductRead, Code = "product:read", Name = "查看产品" },
            new Permission { Id = SeedData.PermSystemUserManage, Code = "system:user:manage", Name = "管理用户" },
            new Permission { Id = SeedData.PermSystemRoleManage, Code = "system:role:manage", Name = "管理角色与权限" }
        );

        // ── 角色 ──
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = SeedData.AdminRoleId, Name = "管理员", Code = "admin", Description = "系统超级管理员，拥有全部权限" },
            new Role { Id = SeedData.DeveloperRoleId, Name = "开发员", Code = "developer", Description = "面料开发相关权限" }
        );

        // ── 用户 ──
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = SeedData.AdminUserId,
                Username = "admin",
                PasswordHash = SeedData.AdminPasswordHash,
                DisplayName = "管理员",
                IsActive = true,
                CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            }
        );

        // ── user_roles: admin 用户 → admin 角色 ──
        modelBuilder.Entity<User>()
            .HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity<Dictionary<string, object>>(
                "user_roles",
                j => j.HasData(new { user_id = SeedData.AdminUserId, role_id = SeedData.AdminRoleId })
            );

        // ── role_permissions: developer 角色 → 开发相关权限 ──
        // admin 角色通过通配 * 拥有全部权限（AuthService 特殊处理），不绑定权限
        var developerPerms = new[]
        {
            SeedData.PermFabricRead, SeedData.PermFabricWrite, SeedData.PermMaterialRead,
            SeedData.PermEquipmentRead, SeedData.PermCustomerRead, SeedData.PermColorRead, SeedData.PermProductRead
        };
        modelBuilder.Entity<Role>()
            .HasMany(r => r.Permissions)
            .WithMany(p => p.Roles)
            .UsingEntity<Dictionary<string, object>>(
                "role_permissions",
                j => j.HasData(developerPerms.Select(p => new { role_id = SeedData.DeveloperRoleId, permission_id = p }).ToArray())
            );
    }
}
