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
            services.RemoveAll<DbContextOptions<OneCupDbContext>>();
            services.RemoveAll<OneCupDbContext>();

            services.AddDbContext<OneCupDbContext>(opt => opt.UseInMemoryDatabase(DatabaseName));

            // ── 2. 关闭限流:测试会高频调用登录端点,避免触发 429。
            //    RemoveAll 移除 AddRateLimiter 注册的全局/策略限制器配置。
            //    注意:RateLimiterService 在未注册 AddRateLimiter 时 UseRateLimiter 为 no-op,
            //    因此即使 controller 带 [EnableRateLimiting] 也不会被拦截。
            services.RemoveAll<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>();
        });
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

        var now = DateTime.UtcNow;

        // ── 权限(仅种子 developer 角色会用到的几项;admin 走通配 * 不需绑定) ──
        var fabricRead = new Permission
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000101"),
            Code = "fabric:read", Name = "查看面料开发", CreatedAt = now
        };
        var fabricWrite = new Permission
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000102"),
            Code = "fabric:write", Name = "录入/编辑面料开发", CreatedAt = now
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
            Permissions = [fabricRead, fabricWrite], CreatedAt = now
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
