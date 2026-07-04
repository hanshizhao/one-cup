using Microsoft.EntityFrameworkCore;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence;

/// <summary>
/// EF Core 数据库上下文。
/// </summary>
public class OneCupDbContext : DbContext
{
    // 种子数据的确定性时间戳（避免 HasData 用动态 DateTime 触发 PendingModelChangesWarning）
    private static readonly DateTime SeedTimestamp = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    public OneCupDbContext(DbContextOptions<OneCupDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<NumberingRule> NumberingRules => Set<NumberingRule>();
    public DbSet<NumberingCounter> NumberingCounters => Set<NumberingCounter>();
    public DbSet<NumberingLog> NumberingLogs => Set<NumberingLog>();
    public DbSet<NumberingTargetType> NumberingTargetTypes => Set<NumberingTargetType>();
    public DbSet<NumberingCategory> NumberingCategories => Set<NumberingCategory>();
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();
    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();

    // ===== Color 模块（feat/color-mgmt）=====
    public DbSet<Color> Colors => Set<Color>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 应用程序集内所有 IEntityTypeConfiguration 配置
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OneCupDbContext).Assembly);

        // 种子数据（必须在 ApplyConfigurationsFromAssembly 之后，复用已配置的实体与关系）
        Seed(modelBuilder);
    }

    /// <summary>
    /// 在 SaveChanges 时自动填充审计字段：新增实体设 CreatedAt，修改实体设 UpdatedAt。
    /// </summary>
    public override int SaveChanges()
    {
        SetAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetAuditFields()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }

    /// <summary>
    /// 种子数据：1 admin 账号、2 角色、16 权限及其关联。
    /// Guid 均为确定性常量（HasData 要求）。
    /// </summary>
    private void Seed(ModelBuilder modelBuilder)
    {
        // ── 权限 ──
        modelBuilder.Entity<Permission>().HasData(
            new Permission { Id = SeedData.PermFabricRead, Code = "fabric:read", Name = "查看面料开发", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermFabricWrite, Code = "fabric:write", Name = "录入/编辑面料开发", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermMaterialRead, Code = "material:read", Name = "查看原料物料", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermMaterialWrite, Code = "material:write", Name = "维护原料物料", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermEquipmentRead, Code = "equipment:read", Name = "查看设备", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermEquipmentWrite, Code = "equipment:write", Name = "维护设备", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermCustomerRead, Code = "customer:read", Name = "查看客户", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermCustomerWrite, Code = "customer:write", Name = "维护客户", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermColorRead, Code = "color:read", Name = "查看颜色对色", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermColorWrite, Code = "color:write", Name = "维护颜色对色", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermProductRead, Code = "product:read", Name = "查看产品", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermProductWrite, Code = "product:write", Name = "录入/编辑产品", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemUserManage, Code = "system:user:manage", Name = "管理用户", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemRoleManage, Code = "system:role:manage", Name = "管理角色与权限", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemNumberingView, Code = "system:numbering:view", Name = "查看编号管理", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemNumberingManage, Code = "system:numbering:manage", Name = "管理编号规则", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemAuditView, Code = "system:audit:view", Name = "查看审计日志", CreatedAt = SeedTimestamp }
        );

        // ── 角色 ──
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = SeedData.AdminRoleId, Name = "管理员", Code = "admin", Description = "系统超级管理员，拥有全部权限", CreatedAt = SeedTimestamp },
            new Role { Id = SeedData.DeveloperRoleId, Name = "开发员", Code = "developer", Description = "面料开发相关权限", CreatedAt = SeedTimestamp }
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
                CreatedAt = SeedTimestamp,
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
            SeedData.PermEquipmentRead, SeedData.PermCustomerRead, SeedData.PermColorRead, SeedData.PermProductRead,
            SeedData.PermSystemAuditView
        };
        modelBuilder.Entity<Role>()
            .HasMany(r => r.Permissions)
            .WithMany(p => p.Roles)
            .UsingEntity<Dictionary<string, object>>(
                "role_permissions",
                j => j.HasData(developerPerms.Select(p => new { role_id = SeedData.DeveloperRoleId, permission_id = p }).ToArray())
            );

        // ── 编号业务类型字典（6 个默认类型，code 与 NumberTargetTypes 常量一致，保证存量数据无缝兼容）──
        modelBuilder.Entity<NumberingTargetType>().HasData(
            new NumberingTargetType { Id = SeedData.TargetTypeFabric, Code = "fabric", NameZh = "面料", NameEn = "Fabric", SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new NumberingTargetType { Id = SeedData.TargetTypeMaterial, Code = "material", NameZh = "原料", NameEn = "Material", SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            new NumberingTargetType { Id = SeedData.TargetTypeEquipment, Code = "equipment", NameZh = "设备", NameEn = "Equipment", SortOrder = 3, IsActive = true, CreatedAt = SeedTimestamp },
            new NumberingTargetType { Id = SeedData.TargetTypeCustomer, Code = "customer", NameZh = "客户", NameEn = "Customer", SortOrder = 4, IsActive = true, CreatedAt = SeedTimestamp },
            new NumberingTargetType { Id = SeedData.TargetTypeColor, Code = "color", NameZh = "颜色", NameEn = "Color", SortOrder = 5, IsActive = true, CreatedAt = SeedTimestamp },
            new NumberingTargetType { Id = SeedData.TargetTypeProduct, Code = "product", NameZh = "产品", NameEn = "Product", SortOrder = 6, IsActive = true, CreatedAt = SeedTimestamp }
        );
    }
}
