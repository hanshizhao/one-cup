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

    // ===== Unit 模块 =====
    public DbSet<MeasurementUnit> MeasurementUnits => Set<MeasurementUnit>();

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

        // ===== Unit 模块：计量单位 =====
        modelBuilder.Entity<Permission>().HasData(
            new Permission { Id = SeedData.PermUnitRead, Code = "system:unit:view", Name = "查看计量单位", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermUnitWrite, Code = "system:unit:manage", Name = "管理计量单位", CreatedAt = SeedTimestamp }
        );

        // 20 个默认单位（6 类，每类一个基准 factor=1）
        modelBuilder.Entity<MeasurementUnit>().HasData(
            // LENGTH 长度
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010001"), Code = "meter", NameZh = "米", NameEn = "Meter", Symbol = "m", Category = "LENGTH", IsBase = true, Factor = 1m, Precision = 2, SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010002"), Code = "decimeter", NameZh = "分米", NameEn = "Decimeter", Symbol = "dm", Category = "LENGTH", IsBase = false, Factor = 0.1m, Precision = 2, SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010003"), Code = "centimeter", NameZh = "厘米", NameEn = "Centimeter", Symbol = "cm", Category = "LENGTH", IsBase = false, Factor = 0.01m, Precision = 2, SortOrder = 3, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010004"), Code = "yard", NameZh = "码", NameEn = "Yard", Symbol = "yd", Category = "LENGTH", IsBase = false, Factor = 0.9144m, Precision = 2, SortOrder = 4, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010005"), Code = "foot", NameZh = "英尺", NameEn = "Foot", Symbol = "ft", Category = "LENGTH", IsBase = false, Factor = 0.3048m, Precision = 2, SortOrder = 5, IsActive = true, CreatedAt = SeedTimestamp },
            // WEIGHT 重量
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010010"), Code = "kilogram", NameZh = "千克", NameEn = "Kilogram", Symbol = "kg", Category = "WEIGHT", IsBase = true, Factor = 1m, Precision = 2, SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010011"), Code = "gram", NameZh = "克", NameEn = "Gram", Symbol = "g", Category = "WEIGHT", IsBase = false, Factor = 0.001m, Precision = 2, SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010012"), Code = "ton", NameZh = "吨", NameEn = "Ton", Symbol = "t", Category = "WEIGHT", IsBase = false, Factor = 1000m, Precision = 2, SortOrder = 3, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010013"), Code = "pound", NameZh = "磅", NameEn = "Pound", Symbol = "lb", Category = "WEIGHT", IsBase = false, Factor = 0.453592m, Precision = 2, SortOrder = 4, IsActive = true, CreatedAt = SeedTimestamp },
            // AREA 面积
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010020"), Code = "square_meter", NameZh = "平方米", NameEn = "Square Meter", Symbol = "㎡", Category = "AREA", IsBase = true, Factor = 1m, Precision = 2, SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010021"), Code = "square_yard", NameZh = "平方码", NameEn = "Square Yard", Symbol = "yd²", Category = "AREA", IsBase = false, Factor = 0.836127m, Precision = 2, SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            // COUNT 数量
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010030"), Code = "piece", NameZh = "件", NameEn = "Piece", Symbol = "件", Category = "COUNT", IsBase = true, Factor = 1m, Precision = 0, SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010031"), Code = "roll", NameZh = "卷", NameEn = "Roll", Symbol = "卷", Category = "COUNT", IsBase = false, Factor = 1m, Precision = 0, SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010032"), Code = "bolt", NameZh = "匹", NameEn = "Bolt", Symbol = "匹", Category = "COUNT", IsBase = false, Factor = 1m, Precision = 0, SortOrder = 3, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010033"), Code = "set", NameZh = "套", NameEn = "Set", Symbol = "套", Category = "COUNT", IsBase = false, Factor = 1m, Precision = 0, SortOrder = 4, IsActive = true, CreatedAt = SeedTimestamp },
            // VOLUME 体积
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010040"), Code = "liter", NameZh = "升", NameEn = "Liter", Symbol = "L", Category = "VOLUME", IsBase = true, Factor = 1m, Precision = 2, SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010041"), Code = "milliliter", NameZh = "毫升", NameEn = "Milliliter", Symbol = "mL", Category = "VOLUME", IsBase = false, Factor = 0.001m, Precision = 2, SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            // YARN 纱线（定长制）
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010050"), Code = "tex", NameZh = "特", NameEn = "Tex", Symbol = "tex", Category = "YARN", IsBase = true, Factor = 1m, Precision = 2, SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010051"), Code = "dtex", NameZh = "分特", NameEn = "Decitex", Symbol = "dtex", Category = "YARN", IsBase = false, Factor = 10m, Precision = 2, SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010052"), Code = "denier", NameZh = "旦尼尔", NameEn = "Denier", Symbol = "D", Category = "YARN", IsBase = false, Factor = 9m, Precision = 2, SortOrder = 3, IsActive = true, CreatedAt = SeedTimestamp }
        );
    }
}
