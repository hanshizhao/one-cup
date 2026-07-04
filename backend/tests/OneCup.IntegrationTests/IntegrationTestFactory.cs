using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OneCup.Domain.Entities;
using OneCup.Infrastructure.Persistence;

namespace OneCup.IntegrationTests;

/// <summary>
/// 集成测试用 WebApplicationFactory:
/// - 用 EF Core InMemory 替换真实 PostgreSQL(管道行为测试不依赖真实 DB);
/// - 设环境为 Testing,加载 appsettings.Testing.json(合规 Jwt 配置);
/// - 关闭限流(避免高频登录触发 429);
/// - 种子 admin + developer 用户,供登录拿 token。
/// </summary>
public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    // 与 SeedData 一致的确定性 Guid,保证 JWT sub claim 与权限计算一致。
    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid DeveloperUserId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    public static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid DeveloperRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    // admin 密码 Admin@123 的 BCrypt 哈希(与 SeedData.AdminPasswordHash 一致,workFactor 12)。
    // 为方便测试,developer 复用同一哈希(即 developer 也可用 Admin@123 登录)。
    public const string PasswordHash = "$2a$12$Q.gT.FJroDeCmWFH6dHJcOdjxPIQgST/nEYCECypvJsLxj5wDQoSi";

    public const string AdminUsername = "admin";
    public const string DeveloperUsername = "developer";
    public const string TestPassword = "Admin@123";

    /// <summary>测试用的 InMemory 数据库名(每个 factory 实例独立,隔离状态)。</summary>
    public string DatabaseName { get; } = $"itest-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // ── 1. 移除真实 DbContext 注册,换 InMemory ──
            // 直接 AddDbContext(UseInMemory) 在已注册 Npgsql 的容器上会触发
            // "Only a single database provider can be registered":因为 Npgsql 在 DI 中
            // 还注册了 provider 级单例(IDatabaseProvider 等),与 InMemory 冲突。
            // 解决:把所有 EF Core / DbContext 相关描述符连根拔起,再干净地注册 InMemory。
            RemoveDbContextRegistrations(services);

            services.AddDbContext<OneCupDbContext>(opt => opt.UseInMemoryDatabase(DatabaseName));

            // ── 2. 关闭限流:测试会高频调用登录端点,避免触发 429。
            //    RemoveAll 移除 AddRateLimiter 注册的全局/策略限制器配置。
            //    注意:RateLimiterService 在未注册 AddRateLimiter 时 UseRateLimiter 为 no-op,
            //    因此即使 controller 带 [EnableRateLimiting] 也不会被拦截。
            services.RemoveAll<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>();
        });
    }

    /// <summary>
    /// 清除容器中所有与 OneCupDbContext / EF Core provider 相关的注册描述符。
    /// AddDbContext 除了注册 DbContext/DbContextOptions,还会通过 provider 扩展
    /// 注册 IDatabaseProvider 等单例;两个 provider 共存会触发启动期异常,
    /// 故需按 ServiceType/ImplementationType 字符串全量过滤后重建。
    /// </summary>
    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var d = services[i];
            var serviceType = d.ServiceType?.FullName;
            var implType = d.ImplementationType?.FullName
                           ?? d.ImplementationInstance?.GetType().FullName;

            // 命中 DbContext 本体、DbContextOptions(泛型/非泛型)、provider 扩展单例。
            if (serviceType is null) continue;
            var hit = serviceType == typeof(OneCupDbContext).FullName
                      || serviceType == "Microsoft.EntityFrameworkCore.DbContextOptions`1"
                      || serviceType == "Microsoft.EntityFrameworkCore.DbContextOptions"
                      || serviceType == "Microsoft.EntityFrameworkCore.DbContextOptions`1[[OneCup.Infrastructure.Persistence.OneCupDbContext, OneCup.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"
                      || serviceType.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal)
                      || serviceType.StartsWith("Npgsql.EntityFrameworkCore.PostgreSQL.", StringComparison.Ordinal)
                      || serviceType.StartsWith("Microsoft.Data.Sqlite.", StringComparison.Ordinal)
                      || (implType is not null
                          && (implType.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal)
                              || implType.StartsWith("Npgsql.EntityFrameworkCore.PostgreSQL.", StringComparison.Ordinal)));
            if (hit) services.RemoveAt(i);
        }
    }

    /// <summary>
    /// 在 InMemory 数据库中种子 admin + developer 用户(含角色/权限),并确保导航属性可被 Include 加载。
    /// 在每个测试类构造时显式调用。
    /// 注意:不调用 EnsureCreated —— InMemory 上的 HasData(OnModelCreating)对多对多跳表导航属性
    /// 存在已知限制,故采用与单元测试一致的手动种子方式(InMemory 库首次使用即惰性创建)。
    /// </summary>
    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneCupDbContext>();

        // 幂等:IClassFixture 下多个测试共享同一 factory(同一 InMemory 库),
        // 构造函数会多次调用 SeedAsync,首次之后直接返回,避免主键冲突。
        // 注:不调用 EnsureCreated —— HasData 种子仅在 EnsureCreated/Migrate 时写入,
        // 此处完全采用手动种子(与单元测试一致),InMemory 库在首次写入时惰性创建。
        if (await db.Users.AnyAsync(u => u.Id == AdminUserId || u.Id == DeveloperUserId))
        {
            return;
        }

        var now = DateTime.UtcNow;

        // ── 权限(种子 developer 角色用到的 10 项,与 SeedData/OneCupDbContext 的 post-refine 集合一致;
        //    admin 走通配 * 不需绑定)。Guid 与 SeedData 对齐,保证一致性。──
        var developerPerms = new[]
        {
            new Permission { Id = Guid.Parse("00000000-0000-0000-0000-000000000301"), Code = "fabric:read", Name = "查看面料开发", CreatedAt = now },
            new Permission { Id = Guid.Parse("00000000-0000-0000-0000-000000000302"), Code = "fabric:create", Name = "录入面料开发", CreatedAt = now },
            new Permission { Id = Guid.Parse("00000000-0000-0000-0000-000000000303"), Code = "fabric:update", Name = "编辑面料开发", CreatedAt = now },
            new Permission { Id = Guid.Parse("00000000-0000-0000-0000-000000000304"), Code = "fabric:delete", Name = "删除面料开发", CreatedAt = now },
            new Permission { Id = Guid.Parse("00000000-0000-0000-0000-000000000305"), Code = "material:read", Name = "查看原料物料", CreatedAt = now },
            new Permission { Id = Guid.Parse("00000000-0000-0000-0000-000000000309"), Code = "equipment:read", Name = "查看设备", CreatedAt = now },
            new Permission { Id = Guid.Parse("00000000-0000-0000-0000-00000000030d"), Code = "customer:read", Name = "查看客户", CreatedAt = now },
            new Permission { Id = Guid.Parse("00000000-0000-0000-0000-000000000311"), Code = "color:read", Name = "查看颜色对色", CreatedAt = now },
            new Permission { Id = Guid.Parse("00000000-0000-0000-0000-000000000315"), Code = "product:read", Name = "查看产品", CreatedAt = now },
            new Permission { Id = Guid.Parse("00000000-0000-0000-0000-00000000032a"), Code = "system:audit:read", Name = "查看审计日志", CreatedAt = now }
        };

        // ── 角色 ──
        var adminRole = new Role
        {
            Id = AdminRoleId,
            Name = "管理员", Code = "admin",
            Description = "系统超级管理员", Permissions = [], CreatedAt = now
        };
        var developerRole = new Role
        {
            Id = DeveloperRoleId,
            Name = "开发员", Code = "developer",
            Description = "面料开发相关权限",
            Permissions = [..developerPerms], CreatedAt = now
        };

        // ── 用户 ──
        var adminUser = new User
        {
            Id = AdminUserId,
            Username = AdminUsername,
            PasswordHash = PasswordHash,
            DisplayName = "管理员",
            IsActive = true,
            Roles = [adminRole],
            CreatedAt = now
        };
        var developerUser = new User
        {
            Id = DeveloperUserId,
            Username = DeveloperUsername,
            PasswordHash = PasswordHash,
            DisplayName = "开发员",
            IsActive = true,
            Roles = [developerRole],
            CreatedAt = now
        };

        db.Users.AddRange(adminUser, developerUser);
        await db.SaveChangesAsync();
        // 清空 ChangeTracker,确保后续查询从 store 重新加载(含导航属性)。
        db.ChangeTracker.Clear();
    }
}
