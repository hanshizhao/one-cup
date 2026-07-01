# 认证全链路实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 打通"前端登录 → 后端颁发 JWT → 后端校验 → 前端据权限渲染"的完整认证闭环。

**Architecture:** 后端 Clean Architecture 四层落地 RBAC（用户/角色/权限）+ JWT（Access 30min + Refresh 7d opaque token 存库）。前端从零搭建 axios 封装 + token 管理，改造 mock 登录为真实链路。

**Tech Stack:** .NET 10 / EF Core 10 / Npgsql / BCrypt.Net-Next / JwtBearer；React 17 / axios 0.24 / TypeScript / Arco Design Pro

## Global Constraints

- 后端依赖方向严格单向：Api → Application → Domain，Infrastructure → Application → Domain，Domain 零依赖
- 数据库表名/列名统一 snake_case（PostgreSQL 惯例）
- 所有实体继承 `BaseEntity`（含 `Id`/`CreatedAt`/`UpdatedAt`）
- 枚举序列化为字符串（Api 层已配置 `JsonStringEnumConverter`）
- 密码用 BCrypt 哈希，绝不明文存储
- Jwt SecretKey 走 user-secrets，不进 git
- 前端 token 存 localStorage，key 为 `onecup_access_token` / `onecup_refresh_token`
- 种子账号：`admin` / `Admin@123`
- Access Token 30 分钟，Refresh Token 7 天
- admin 角色通过通配 `*` 拥有全部权限（在 AuthService 中特殊处理，不逐条绑定）

## File Structure

**后端新建文件：**
```
backend/src/OneCup.Domain/
  Entities/User.cs
  Entities/Role.cs
  Entities/Permission.cs
  Entities/RefreshToken.cs

backend/src/OneCup.Application/
  Options/JwtOptions.cs
  Interfaces/IAuthService.cs
  Interfaces/IJwtTokenService.cs
  Interfaces/IPasswordHasher.cs
  Dtos/Auth/LoginRequest.cs
  Dtos/Auth/RefreshRequest.cs
  Dtos/Auth/TokenResponse.cs
  Dtos/Auth/CurrentUser.cs

backend/src/OneCup.Infrastructure/
  Persistence/Configurations/UserConfiguration.cs
  Persistence/Configurations/RoleConfiguration.cs
  Persistence/Configurations/PermissionConfiguration.cs
  Persistence/Configurations/RefreshTokenConfiguration.cs
  Persistence/SeedData.cs
  Services/PasswordHasher.cs
  Services/JwtTokenService.cs
  Services/AuthService.cs

backend/src/OneCup.Api/
  Controllers/AuthController.cs
  Services/CurrentUserService.cs
  Migrations/                          # EF Core 生成

backend/tests/OneCup.UnitTests/
  Auth/PasswordHasherTests.cs
  Auth/AuthServiceTests.cs
  Auth/JwtTokenServiceTests.cs
```

**后端修改文件：**
```
backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj   # +BCrypt
backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs # +DbSet +配置 +种子
backend/src/OneCup.Api/OneCup.Api.csproj                          # +JwtBearer
backend/src/OneCup.Api/Program.cs                                 # +认证授权DI
backend/src/OneCup.Api/appsettings.json                           # +Jwt节
backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj            # +项目引用
```

**前端新建文件：**
```
frontend/.env.development
frontend/.env.production
frontend/src/api/request.ts
frontend/src/api/auth.ts
frontend/src/utils/token.ts
```

**前端修改文件：**
```
frontend/src/utils/checkLogin.tsx
frontend/src/pages/login/form.tsx
frontend/src/main.tsx
frontend/src/components/NavBar/index.tsx
frontend/src/mock/user.ts
frontend/src/store/index.ts
```

---

## Task 1: Domain 层 — RBAC 实体

**Files:**
- Create: `backend/src/OneCup.Domain/Entities/User.cs`
- Create: `backend/src/OneCup.Domain/Entities/Role.cs`
- Create: `backend/src/OneCup.Domain/Entities/Permission.cs`
- Create: `backend/src/OneCup.Domain/Entities/RefreshToken.cs`

**Interfaces:**
- Consumes: `BaseEntity`（已有，`Id`/`CreatedAt`/`UpdatedAt`）
- Produces: `User`、`Role`、`Permission`、`RefreshToken` 实体类型，供 Infrastructure 配置与 Application DTO 引用

- [ ] **Step 1: 创建 Permission 实体**

```csharp
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
```

- [ ] **Step 2: 创建 Role 实体**

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 角色，聚合多个权限。
/// </summary>
public class Role : BaseEntity
{
    /// <summary>显示名（如"管理员"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>角色编码（如 admin）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>描述</summary>
    public string? Description { get; set; }

    /// <summary>此角色下的用户集合（多对多）</summary>
    public List<User> Users { get; set; } = [];

    /// <summary>此角色拥有的权限集合（多对多）</summary>
    public List<Permission> Permissions { get; set; } = [];
}
```

- [ ] **Step 3: 创建 User 实体**

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 系统用户。
/// </summary>
public class User : BaseEntity
{
    /// <summary>登录用户名（唯一）</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>BCrypt 哈希后的密码</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>显示名</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>邮箱</summary>
    public string? Email { get; set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>用户的角色集合（多对多）</summary>
    public List<Role> Roles { get; set; } = [];

    /// <summary>用户的刷新令牌集合（一对多）</summary>
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
```

- [ ] **Step 4: 创建 RefreshToken 实体**

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 刷新令牌（opaque，存数据库，支持吊销）。
/// </summary>
public class RefreshToken : BaseEntity
{
    /// <summary>令牌字符串（随机 opaque）</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>归属用户 Id</summary>
    public Guid UserId { get; set; }

    /// <summary>归属用户（导航属性）</summary>
    public User User { get; set; } = null!;

    /// <summary>过期时间</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>是否已吊销</summary>
    public bool IsRevoked { get; set; } = false;
}
```

- [ ] **Step 5: 验证编译**

Run: `dotnet build backend/src/OneCup.Domain`
Expected: BUILD SUCCEEDED，0 errors

- [ ] **Step 6: Commit**

```bash
git add backend/src/OneCup.Domain/Entities/
git commit -m "feat(domain): 添加 RBAC 实体 (User/Role/Permission/RefreshToken)"
```

---

## Task 2: Application 层 — 接口、DTO 与配置

**Files:**
- Create: `backend/src/OneCup.Application/Options/JwtOptions.cs`
- Create: `backend/src/OneCup.Application/Dtos/Auth/LoginRequest.cs`
- Create: `backend/src/OneCup.Application/Dtos/Auth/RefreshRequest.cs`
- Create: `backend/src/OneCup.Application/Dtos/Auth/TokenResponse.cs`
- Create: `backend/src/OneCup.Application/Dtos/Auth/CurrentUser.cs`
- Create: `backend/src/OneCup.Application/Interfaces/IJwtTokenService.cs`
- Create: `backend/src/OneCup.Application/Interfaces/IPasswordHasher.cs`
- Create: `backend/src/OneCup.Application/Interfaces/IAuthService.cs`

**Interfaces:**
- Consumes: `User`/`Role`/`Permission`/`RefreshToken`（Task 1）
- Produces: `JwtOptions`、4 个 DTO、3 个接口（`IAuthService`/`IJwtTokenService`/`IPasswordHasher`），供 Infrastructure 实现与 Api 调用

- [ ] **Step 1: 创建 JwtOptions**

```csharp
namespace OneCup.Application.Options;

/// <summary>
/// JWT 相关配置，绑定 appsettings 的 Jwt 节。
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Access Token 有效期（分钟）</summary>
    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>Refresh Token 有效期（天）</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
```

- [ ] **Step 2: 创建 LoginRequest / RefreshRequest DTO**

```csharp
namespace OneCup.Application.Dtos.Auth;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
```

```csharp
namespace OneCup.Application.Dtos.Auth;

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
```

- [ ] **Step 3: 创建 TokenResponse DTO**

```csharp
namespace OneCup.Application.Dtos.Auth;

/// <summary>
/// 登录/刷新成功后返回的令牌对。
/// </summary>
public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Access Token 有效期（秒）</summary>
    public int ExpiresIn { get; set; }
}
```

- [ ] **Step 4: 创建 CurrentUser DTO**

```csharp
namespace OneCup.Application.Dtos.Auth;

/// <summary>
/// 当前登录用户信息（GET /api/auth/me 返回）。
/// </summary>
public class CurrentUser
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>角色编码集合</summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>权限编码集合（admin 为 ["*"]）</summary>
    public List<string> Permissions { get; set; } = [];
}
```

- [ ] **Step 5: 创建 IPasswordHasher 接口**

```csharp
namespace OneCup.Application.Interfaces;

/// <summary>
/// 密码哈希服务接口。
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
```

- [ ] **Step 6: 创建 IJwtTokenService 接口**

```csharp
using OneCup.Domain.Entities;

namespace OneCup.Application.Interfaces;

/// <summary>
/// JWT 签发服务接口。
/// </summary>
public interface IJwtTokenService
{
    /// <summary>为指定用户签发 Access Token，返回 token 字符串。</summary>
    string GenerateAccessToken(User user);

    /// <summary>生成随机 opaque Refresh Token 字符串（不签 JWT）。</summary>
    string GenerateRefreshToken();
}
```

- [ ] **Step 7: 创建 IAuthService 接口**

```csharp
using OneCup.Application.Dtos.Auth;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 认证服务接口，编排登录/刷新/登出/获取当前用户。
/// </summary>
public interface IAuthService
{
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);

    Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);

    Task LogoutAsync(Guid userId, CancellationToken ct = default);

    Task<CurrentUser?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}
```

- [ ] **Step 8: 验证编译**

Run: `dotnet build backend/src/OneCup.Application`
Expected: BUILD SUCCEEDED，0 errors

- [ ] **Step 9: Commit**

```bash
git add backend/src/OneCup.Application/
git commit -m "feat(application): 添加认证接口、DTO 与 JwtOptions"
```

---

## Task 3: Infrastructure — EF Core 实体配置与 DbContext

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/RoleConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/PermissionConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`

**Interfaces:**
- Consumes: 4 个实体（Task 1）
- Produces: 配置好的 `OneCupDbContext`（含 DbSet + snake_case + 关联关系），供种子数据（Task 5）和 AuthService（Task 8）使用

- [ ] **Step 1: 创建 UserConfiguration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        builder.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(50).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(100);
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(u => u.Username).IsUnique();

        builder.HasMany(u => u.Roles)
            .WithMany(r => r.Users)
            .UsingEntity<Dictionary<string, object>>(
                "user_roles",
                j => j.HasOne<Role>().WithMany().HasForeignKey("role_id"),
                j => j.HasOne<User>().WithMany().HasForeignKey("user_id"),
                j => j.HasKey("user_id", "role_id"));

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId);
    }
}
```

- [ ] **Step 2: 创建 RoleConfiguration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(r => r.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(r => r.Description).HasColumnName("description").HasMaxLength(200);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => r.Name).IsUnique();
        builder.HasIndex(r => r.Code).IsUnique();

        builder.HasMany(r => r.Permissions)
            .WithMany(p => p.Roles)
            .UsingEntity<Dictionary<string, object>>(
                "role_permissions",
                j => j.HasOne<Permission>().WithMany().HasForeignKey("permission_id"),
                j => j.HasOne<Role>().WithMany().HasForeignKey("role_id"),
                j => j.HasKey("role_id", "permission_id"));
    }
}
```

- [ ] **Step 3: 创建 PermissionConfiguration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(200);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(p => p.Code).IsUnique();
    }
}
```

- [ ] **Step 4: 创建 RefreshTokenConfiguration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id).HasColumnName("id");
        builder.Property(rt => rt.Token).HasColumnName("token").HasMaxLength(64).IsRequired();
        builder.Property(rt => rt.UserId).HasColumnName("user_id");
        builder.Property(rt => rt.ExpiresAt).HasColumnName("expires_at");
        builder.Property(rt => rt.IsRevoked).HasColumnName("is_revoked").IsRequired();
        builder.Property(rt => rt.CreatedAt).HasColumnName("created_at");
        builder.Property(rt => rt.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(rt => rt.Token).IsUnique();
    }
}
```

- [ ] **Step 5: 改造 OneCupDbContext（注册 DbSet + 应用配置）**

将 `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs` 完整替换为：

```csharp
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
```

- [ ] **Step 6: 验证编译**

Run: `dotnet build backend/src/OneCup.Infrastructure`
Expected: BUILD SUCCEEDED，0 errors

- [ ] **Step 7: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/
git commit -m "feat(infra): 添加 RBAC 实体的 EF Core 配置并注册 DbSet"
```

---

## Task 4: Infrastructure — BCrypt 密码哈希服务 + 预算 admin 哈希

> **任务顺序说明：** 先完成本任务（装 BCrypt + 实现 PasswordHasher + 算出 admin 哈希），再做 Task 5 种子数据，这样种子数据可直接填入真实哈希，避免提交占位符。

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj`
- Create: `backend/src/OneCup.Infrastructure/Services/PasswordHasher.cs`

**Interfaces:**
- Consumes: `IPasswordHasher`（Task 2）
- Produces: `PasswordHasher` 实现 + 安装好的 `BCrypt.Net-Next` 包 + 一个固定 BCrypt 哈希值（供 Task 5 种子数据使用）

- [ ] **Step 1: 安装 BCrypt.Net-Next 包**

Run:
```bash
cd backend/src/OneCup.Infrastructure
dotnet add package BCrypt.Net-Next
```
Expected: `PackageReference for BCrypt.Net-Next` added

- [ ] **Step 2: 创建 PasswordHasher 实现**

```csharp
using OneCup.Application.Interfaces;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// BCrypt 密码哈希实现。
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);
}
```

- [ ] **Step 3: 预计算 admin 密码哈希**

创建临时控制台项目生成哈希：

```bash
mkdir -p backend/HashGen && cd backend/HashGen
dotnet new console --force
dotnet add package BCrypt.Net-Next
```

把 `backend/HashGen/Program.cs` 内容替换为：

```csharp
Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("Admin@123", workFactor: 12));
```

Run:
```bash
cd backend/HashGen && dotnet run
```
复制输出的 `$2a$12$...` 哈希字符串（约 60 字符），**记下来**，Task 5 要用。

清理临时项目：
```bash
rm -rf backend/HashGen
```

- [ ] **Step 4: 验证编译**

Run: `dotnet build backend/src/OneCup.Infrastructure`
Expected: BUILD SUCCEEDED

- [ ] **Step 5: Commit**

```bash
git add backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj backend/src/OneCup.Infrastructure/Services/PasswordHasher.cs
git commit -m "feat(infra): BCrypt 密码哈希服务"
```

---

## Task 5: Infrastructure — 种子数据

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`

**Interfaces:**
- Consumes: `OneCupDbContext`（Task 3）、Task 4 Step 3 生成的 admin 哈希
- Produces: 迁移后自动入库的 admin 账号、2 角色、13 权限

**注意：** 种子数据的 Guid 必须是确定性常量值（EF Core `HasData` 要求）。

- [ ] **Step 1: 创建 SeedData 静态类**

```csharp
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence;

/// <summary>
/// 种子数据常量。Guid 使用确定性值（HasData 要求主键固定）。
/// </summary>
internal static class SeedData
{
    // 固定 Guid（确定性）
    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid DeveloperRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    // 权限 Guid：第 4 段从 101 开始递增
    public static readonly Guid PermFabricRead = Guid.Parse("00000000-0000-0000-0000-000000000101");
    public static readonly Guid PermFabricWrite = Guid.Parse("00000000-0000-0000-0000-000000000102");
    public static readonly Guid PermMaterialRead = Guid.Parse("00000000-0000-0000-0000-000000000103");
    public static readonly Guid PermMaterialWrite = Guid.Parse("00000000-0000-0000-0000-000000000104");
    public static readonly Guid PermEquipmentRead = Guid.Parse("00000000-0000-0000-0000-000000000105");
    public static readonly Guid PermEquipmentWrite = Guid.Parse("00000000-0000-0000-0000-000000000106");
    public static readonly Guid PermCustomerRead = Guid.Parse("00000000-0000-0000-0000-000000000107");
    public static readonly Guid PermCustomerWrite = Guid.Parse("00000000-0000-0000-0000-000000000108");
    public static readonly Guid PermColorRead = Guid.Parse("00000000-0000-0000-0000-000000000109");
    public static readonly Guid PermColorWrite = Guid.Parse("00000000-0000-0000-0000-000000000110");
    public static readonly Guid PermProductRead = Guid.Parse("00000000-0000-0000-0000-000000000111");
    public static readonly Guid PermSystemUserManage = Guid.Parse("00000000-0000-0000-0000-000000000112");
    public static readonly Guid PermSystemRoleManage = Guid.Parse("00000000-0000-0000-0000-000000000113");

    /// <summary>
    /// admin 密码 Admin@123 的 BCrypt 哈希。
    /// 此值由 Task 4 Step 3 预计算生成，直接粘贴此处（$2a$12$... 格式）。
    /// </summary>
    public const string AdminPasswordHash = "<把 Task 4 Step 3 生成的哈希粘贴到这里>";
}
```

- [ ] **Step 3: 在 DbContext 的 OnModelCreating 末尾追加种子数据**

在 `OneCupDbContext.OnModelCreating` 中，`ApplyConfigurationsFromAssembly` 之后追加调用：

```csharp
        Seed(modelBuilder);
    }

    private void Seed(ModelBuilder modelBuilder)
    {
        var s = SeedData;

        // ── 权限 ──
        modelBuilder.Entity<Permission>().HasData(
            new Permission { Id = s.PermFabricRead, Code = "fabric:read", Name = "查看面料开发" },
            new Permission { Id = s.PermFabricWrite, Code = "fabric:write", Name = "录入/编辑面料开发" },
            new Permission { Id = s.PermMaterialRead, Code = "material:read", Name = "查看原料物料" },
            new Permission { Id = s.PermMaterialWrite, Code = "material:write", Name = "维护原料物料" },
            new Permission { Id = s.PermEquipmentRead, Code = "equipment:read", Name = "查看设备" },
            new Permission { Id = s.PermEquipmentWrite, Code = "equipment:write", Name = "维护设备" },
            new Permission { Id = s.PermCustomerRead, Code = "customer:read", Name = "查看客户" },
            new Permission { Id = s.PermCustomerWrite, Code = "customer:write", Name = "维护客户" },
            new Permission { Id = s.PermColorRead, Code = "color:read", Name = "查看颜色对色" },
            new Permission { Id = s.PermColorWrite, Code = "color:write", Name = "维护颜色对色" },
            new Permission { Id = s.PermProductRead, Code = "product:read", Name = "查看产品" },
            new Permission { Id = s.PermSystemUserManage, Code = "system:user:manage", Name = "管理用户" },
            new Permission { Id = s.PermSystemRoleManage, Code = "system:role:manage", Name = "管理角色与权限" }
        );

        // ── 角色 ──
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = s.AdminRoleId, Name = "管理员", Code = "admin", Description = "系统超级管理员，拥有全部权限" },
            new Role { Id = s.DeveloperRoleId, Name = "开发员", Code = "developer", Description = "面料开发相关权限" }
        );

        // ── 用户 ──
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = s.AdminUserId,
                Username = "admin",
                PasswordHash = s.AdminPasswordHash,
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
                j => j.HasData(new { user_id = s.AdminUserId, role_id = s.AdminRoleId })
            );

        // ── role_permissions: developer 角色 → 开发相关权限 ──
        // admin 角色通过通配 * 拥有全部权限（AuthService 特殊处理），不绑定权限
        var developerPerms = new[]
        {
            s.PermFabricRead, s.PermFabricWrite, s.PermMaterialRead,
            s.PermEquipmentRead, s.PermCustomerRead, s.PermColorRead, s.PermProductRead
        };
        modelBuilder.Entity<Role>()
            .HasMany(r => r.Permissions)
            .WithMany(p => p.Roles)
            .UsingEntity<Dictionary<string, object>>(
                "role_permissions",
                j => j.HasData(developerPerms.Select(p => new { role_id = s.DeveloperRoleId, permission_id = p }).ToArray())
            );
    }
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build backend/src/OneCup.Infrastructure`
Expected: BUILD SUCCEEDED（确认已粘贴真实哈希值到 `AdminPasswordHash`）

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/
git commit -m "feat(infra): 添加 RBAC 种子数据 (admin 账号/2 角色/13 权限)"
```

---

## Task 6: Unit Test — PasswordHasher

**Files:**
- Modify: `backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj`
- Create: `backend/tests/OneCup.UnitTests/Auth/PasswordHasherTests.cs`

**Interfaces:**
- Consumes: `PasswordHasher`（Task 4）、`IPasswordHasher`
- Produces: 通过的密码哈希单元测试

- [ ] **Step 1: 测试项目添加 Infrastructure 项目引用**

Run:
```bash
cd backend/tests/OneCup.UnitTests
dotnet add reference ../../src/OneCup.Infrastructure/OneCup.Infrastructure.csproj
```

- [ ] **Step 2: 写 PasswordHasher 测试**

```csharp
using OneCup.Infrastructure.Services;

namespace OneCup.UnitTests.Auth;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ReturnsNonEmptyString_DifferentFromInput()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("mypassword");
        Assert.False(string.IsNullOrEmpty(hash));
        Assert.NotEqual("mypassword", hash);
    }

    [Fact]
    public void Hash_GeneratesDifferentHashes_ForSamePassword()
    {
        var hasher = new PasswordHasher();
        var hash1 = hasher.Hash("samepass");
        var hash2 = hasher.Hash("samepass");
        Assert.NotEqual(hash1, hash2); // BCrypt salt 使每次结果不同
    }

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("correctpass");
        Assert.True(hasher.Verify("correctpass", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("correctpass");
        Assert.False(hasher.Verify("wrongpass", hash));
    }
}
```

- [ ] **Step 3: 运行测试**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter PasswordHasher`
Expected: 4 passed, 0 failed

- [ ] **Step 4: Commit**

```bash
git add backend/tests/OneCup.UnitTests/
git commit -m "test: PasswordHasher 单元测试"
```

---

## Task 7: Infrastructure — JWT 签发服务

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Services/JwtTokenService.cs`

**Interfaces:**
- Consumes: `IJwtTokenService`（Task 2）、`JwtOptions`（Task 2）、`User`（Task 1）
- Produces: `JwtTokenService` 实现，供 Task 8 AuthService 与 Api 层 JwtBearer 配置使用

- [ ] **Step 1: 创建 JwtTokenService 实现**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OneCup.Application.Interfaces;
using OneCup.Application.Options;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// JWT 签发服务实现（HS256）。
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateAccessToken(User user)
    {
        var roleCodes = user.Roles.Select(r => r.Code).ToList();
        // admin 角色通配为 *，其他角色聚合权限编码
        var permCodes = roleCodes.Contains("admin")
            ? new List<string> { "*" }
            : user.Roles.SelectMany(r => r.Permissions).Select(p => p.Code).Distinct().ToList();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("username", user.Username),
        };
        claims.AddRange(roleCodes.Select(c => new Claim("role_codes", c)));
        claims.AddRange(permCodes.Select(p => new Claim("perm_codes", p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        // 32 字节随机数 → Base64URL（约 43 字符）
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>获取 token 验证参数，供 Api 层 JwtBearer 复用。</summary>
    public TokenValidationParameters GetValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)),
            ClockSkew = TimeSpan.Zero,
        };
    }
}
```

- [ ] **Step 2: 验证编译**

JwtBearer 包还没装（Task 9 装到 Api 层）。Infrastructure 引用的 `Microsoft.IdentityModel.Tokens` 和 `System.IdentityModel.Tokens.Jwt` 需确认是否随 EF/Npgsql 来。若编译报缺失，执行：

Run:
```bash
cd backend/src/OneCup.Infrastructure
dotnet add package Microsoft.IdentityModel.Tokens
dotnet add package System.IdentityModel.Tokens.Jwt
```
然后 `dotnet build backend/src/OneCup.Infrastructure` → Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Infrastructure/
git commit -m "feat(infra): JWT 签发服务 (HS256 + claims + refresh token 生成)"
```

---

## Task 8: Infrastructure — AuthService（认证业务编排）

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Services/AuthService.cs`

**Interfaces:**
- Consumes: `IAuthService`（Task 2）、`IJwtTokenService`（Task 7）、`IPasswordHasher`（Task 4）、`OneCupDbContext`（Task 3）、`JwtOptions`（Task 2）、`DomainException`
- Produces: `AuthService` 实现，供 Task 9 Api Controller 调用

- [ ] **Step 1: 创建 AuthService 实现**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneCup.Application.Dtos.Auth;
using OneCup.Application.Interfaces;
using OneCup.Application.Options;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// 认证服务实现：编排登录/刷新/登出/获取当前用户。
/// </summary>
public class AuthService : IAuthService
{
    private readonly OneCupDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtOptions _options;

    public AuthService(
        OneCupDbContext db,
        IJwtTokenService jwt,
        IPasswordHasher passwordHasher,
        IOptions<JwtOptions> options)
    {
        _db = db;
        _jwt = jwt;
        _passwordHasher = passwordHasher;
        _options = options.Value;
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Username == request.Username, ct);

        if (user is null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new DomainException("用户名或密码错误");
        }

        return await IssueTokensAsync(user, ct);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var stored = await _db.RefreshTokens
            .Include(rt => rt.User).ThenInclude(u => u.Roles).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, ct);

        if (stored is null || stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow)
        {
            throw new DomainException("刷新令牌无效或已过期");
        }

        // 轮换：吊销旧 token
        stored.IsRevoked = true;
        stored.UpdatedAt = DateTime.UtcNow;

        return await IssueTokensAsync(stored.User, ct);
    }

    public async Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync(ct);

        foreach (var rt in tokens)
        {
            rt.IsRevoked = true;
            rt.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<CurrentUser?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null) return null;

        var roleCodes = user.Roles.Select(r => r.Code).ToList();
        var permCodes = roleCodes.Contains("admin")
            ? new List<string> { "*" }
            : user.Roles.SelectMany(r => r.Permissions).Select(p => p.Code).Distinct().ToList();

        return new CurrentUser
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Roles = roleCodes,
            Permissions = permCodes,
        };
    }

    /// <summary>签发 access + refresh token 对并持久化 refresh token。</summary>
    private async Task<TokenResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = new RefreshToken
        {
            Token = _jwt.GenerateRefreshToken(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays),
            IsRevoked = false,
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresIn = _options.AccessTokenMinutes * 60,
        };
    }
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build backend/src/OneCup.Infrastructure`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Services/AuthService.cs
git commit -m "feat(infra): AuthService 认证业务编排 (登录/刷新/登出/当前用户)"
```

---

## Task 9: Api 层 — JwtBearer 中间件与 DI 配置

**Files:**
- Modify: `backend/src/OneCup.Api/OneCup.Api.csproj`
- Modify: `backend/src/OneCup.Api/Program.cs`
- Modify: `backend/src/OneCup.Api/appsettings.json`
- Create: `backend/src/OneCup.Api/Services/CurrentUserService.cs`

**Interfaces:**
- Consumes: `AuthService`/`JwtTokenService`/`PasswordHasher`（Task 4/7/8）、`IAuthService`/`IJwtTokenService`/`IPasswordHasher`（Task 2）、`JwtOptions`（Task 2）
- Produces: 配置好认证授权的 `Program.cs`、`CurrentUserService`，供 Task 10 Controller 使用

- [ ] **Step 1: 安装 JwtBearer 包**

Run:
```bash
cd backend/src/OneCup.Api
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```
Expected: package added

- [ ] **Step 2: 在 appsettings.json 添加 Jwt 配置节**

在 `appsettings.json` 的根对象内（`Cors` 节之后）追加：

```json
  ,
  "Jwt": {
    "Issuer": "OneCup",
    "Audience": "OneCup",
    "AccessTokenMinutes": 30,
    "RefreshTokenDays": 7,
    "SecretKey": "REPLACE_VIA_USER_SECRETS"
  }
```

> `SecretKey` 值在 appsettings 里只是占位，真实值通过 user-secrets 设置。

- [ ] **Step 3: 设置 Jwt SecretKey 到 user-secrets**

Run:
```bash
cd backend/src/OneCup.Api
dotnet user-secrets set "Jwt:SecretKey" "OneCup_Super_Secret_Key_At_Least_32_Chars_2026!"
```
Expected: `Successfully saved...`

- [ ] **Step 4: 创建 CurrentUserService**

```csharp
using System.Security.Claims;

namespace OneCup.Api.Services;

/// <summary>
/// 从当前 HTTP 上下文的 Claims 中提取用户信息。
/// 供需要"当前用户"的 Controller 注入使用。
/// </summary>
public class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var sub = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Username =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue("username");
}
```

- [ ] **Step 5: 改造 Program.cs（添加认证授权 DI）**

在 `Program.cs` 中，`var builder = WebApplication.CreateBuilder(args);` 之后、`var app = builder.Build();` 之前的 DI 注册区，**追加**以下内容（注意放在已有注册之后）：

```csharp
// ── 认证授权 (JWT) ─────────────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!)),
            NameClaimType = ClaimTypes.Name,
            ClockSkew = TimeSpan.Zero,
        };
    });
builder.Services.AddAuthorization();

// ── 依赖注入:认证相关服务 ─────────────────────────────────────
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<CurrentUserService>();
builder.Services.AddHttpContextAccessor();
```

在文件顶部 `using` 区追加：

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OneCup.Api.Services;
using OneCup.Application.Interfaces;
using OneCup.Application.Options;
using OneCup.Infrastructure.Services;
using System.Security.Claims;
using System.Text;
```

- [ ] **Step 6: 在中间件管道启用认证授权**

在 `Program.cs` 中，`app.UseCors();` 之后、`app.UseExceptionHandler(...)` 之前，追加：

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

- [ ] **Step 7: 验证编译**

Run: `dotnet build backend/src/OneCup.Api`
Expected: BUILD SUCCEEDED

- [ ] **Step 8: Commit**

```bash
git add backend/src/OneCup.Api/
git commit -m "feat(api): 配置 JwtBearer 认证授权与 CurrentUserService"
```

---

## Task 10: Api 层 — AuthController（4 个端点）

**Files:**
- Create: `backend/src/OneCup.Api/Controllers/AuthController.cs`

**Interfaces:**
- Consumes: `IAuthService`（Task 8）、`CurrentUserService`（Task 9）、DTO（Task 2）
- Produces: 4 个 HTTP 端点：`POST /api/auth/login`、`/refresh`、`/logout`、`GET /api/auth/me`

- [ ] **Step 1: 创建 AuthController**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.Auth;
using OneCup.Application.Interfaces;
using OneCup.Api.Services;

namespace OneCup.Api.Controllers;

/// <summary>
/// 认证相关端点。
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly CurrentUserService _current;

    public AuthController(IAuthService authService, CurrentUserService current)
    {
        _authService = authService;
        _current = current;
    }

    /// <summary>用户名密码登录。</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), Status200OK)]
    [ProducesResponseType(typeof(object), Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return Ok(result);
    }

    /// <summary>用刷新令牌换新的访问令牌。</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), Status200OK)]
    [ProducesResponseType(typeof(object), Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshAsync(request, ct);
        return Ok(result);
    }

    /// <summary>登出，吊销当前用户的刷新令牌。</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (_current.UserId is null) return Unauthorized();
        await _authService.LogoutAsync(_current.UserId.Value, ct);
        return NoContent();
    }

    /// <summary>获取当前登录用户信息（含角色与权限）。</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUser), Status200OK)]
    [ProducesResponseType(Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (_current.UserId is null) return Unauthorized();
        var user = await _authService.GetCurrentUserAsync(_current.UserId.Value, ct);
        return user is null ? Unauthorized() : Ok(user);
    }
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build backend/src/OneCup.Api`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Api/Controllers/AuthController.cs
git commit -m "feat(api): AuthController 认证端点 (login/refresh/logout/me)"
```

---

## Task 11: EF Core 迁移与数据库更新

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Migrations/` （EF Core 生成）

**Interfaces:**
- Consumes: 完整的 `OneCupDbContext` + 所有配置 + 种子数据（Task 3/4/5）
- Produces: 数据库中的 6 张表 + 种子数据

- [ ] **Step 1: 生成 InitialCreate 迁移**

Run:
```bash
cd backend/src/OneCup.Api
dotnet ef migrations add InitialCreate --project ../OneCup.Infrastructure --startup-project .
```
Expected: 迁移文件生成在 `OneCup.Infrastructure/Migrations/`，无 error

- [ ] **Step 2: 检查迁移文件**

确认生成的迁移包含：`users`、`roles`、`permissions`、`user_roles`、`role_permissions`、`refresh_tokens` 6 张表的 CreateTable + 种子数据 InsertData。检查 `SeedData.AdminPasswordHash` 已是真实 BCrypt 哈希（Task 4 预算的 `$2a$12$...` 串）。

- [ ] **Step 3: 应用迁移到数据库**

> 前提：云服务器 PostgreSQL（端口 15207）可达，连接字符串已配置。

Run:
```bash
cd backend/src/OneCup.Api
dotnet ef database update --project ../OneCup.Infrastructure --startup-project .
```
Expected: `Done.` 无 error

- [ ] **Step 4: 验证表结构（可选，用 psql 或 pgAdmin）**

确认 6 张表存在，`users` 表有 admin 记录（password_hash 非空）。

- [ ] **Step 5: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Migrations/
git commit -m "feat(infra): InitialCreate 迁移 (6 张 RBAC 表 + 种子数据)"
```

---

## Task 12: 后端集成验证（手动 API 测试）

**Files:** 无（验证 Task 9-11 的产物）

- [ ] **Step 1: 启动后端**

Run:
```bash
cd backend
dotnet run --project src/OneCup.Api
```
Expected: 监听 `http://localhost:5000`（或 launchSettings 配置的端口），无异常

- [ ] **Step 2: 测试登录（curl）**

Run:
```bash
curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123"}'
```
Expected: 200，返回 JSON 含 `accessToken`、`refreshToken`、`expiresIn`。保存 accessToken 与 refreshToken。

- [ ] **Step 3: 测试 /me（带 token）**

Run（用上一步的 accessToken 替换 `<TOKEN>`）：
```bash
curl -s http://localhost:5000/api/auth/me -H "Authorization: Bearer <TOKEN>"
```
Expected: 200，返回含 `"permissions":["*"]`、`"roles":["admin"]`、`"username":"admin"`

- [ ] **Step 4: 测试 refresh**

Run（用上一步保存的 refreshToken 替换 `<REFRESH>`）：
```bash
curl -s -X POST http://localhost:5000/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<REFRESH>"}'
```
Expected: 200，返回新的 token 对（旧 refreshToken 应已失效）

- [ ] **Step 5: 测试 logout**

Run（用新 accessToken 替换 `<TOKEN>`）：
```bash
curl -s -X POST http://localhost:5000/api/auth/logout -H "Authorization: Bearer <TOKEN>" -o /dev/null -w "%{http_code}"
```
Expected: `204`

- [ ] **Step 6: 测试错误密码**

Run:
```bash
curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"wrong"}'
```
Expected: `400`（DomainException → 400）

- [ ] **Step 7: 测试未授权访问**

Run:
```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/api/auth/me
```
Expected: `401`

> 所有步骤通过即后端验证完成。无需 commit（无代码变更）。

---

## Task 13: 前端 — 环境配置与 token 工具

**Files:**
- Create: `frontend/.env.development`
- Create: `frontend/.env.production`
- Create: `frontend/src/utils/token.ts`
- Create: `frontend/src/api/request.ts`
- Create: `frontend/src/api/auth.ts`

**Interfaces:**
- Consumes: 后端 4 个认证端点（Task 10）
- Produces: axios 封装实例 `request`、token 存取工具、认证 API 函数

- [ ] **Step 1: 创建环境变量文件**

`frontend/.env.development`：
```
VITE_API_BASE_URL=http://localhost:5000
```

`frontend/.env.production`：
```
VITE_API_BASE_URL=
```

- [ ] **Step 2: 创建 token 工具**

`frontend/src/utils/token.ts`：
```typescript
const ACCESS_TOKEN_KEY = 'onecup_access_token';
const REFRESH_TOKEN_KEY = 'onecup_refresh_token';

export function getAccessToken(): string | null {
  return localStorage.getItem(ACCESS_TOKEN_KEY);
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_KEY);
}

export function setTokens(accessToken: string, refreshToken: string) {
  localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
  localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
}

export function removeTokens() {
  localStorage.removeItem(ACCESS_TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
}
```

- [ ] **Step 3: 创建 axios 封装**

`frontend/src/api/request.ts`：
```typescript
import axios from 'axios';
import { Message } from '@arco-design/web-react';
import {
  getAccessToken,
  getRefreshToken,
  setTokens,
  removeTokens,
} from '@/utils/token';

const request = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '',
  timeout: 15000,
});

// 不需要 token 的接口
const WHITE_LIST = ['/api/auth/login', '/api/auth/refresh'];

// 并发 refresh 防抖
let isRefreshing = false;
let pendingQueue: Array<(token: string) => void> = [];

// ── 请求拦截器：自动注入 token ──
request.interceptors.request.use((config) => {
  const url = config.url || '';
  if (!WHITE_LIST.some((p) => url.startsWith(p))) {
    const token = getAccessToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  }
  return config;
});

// ── 响应拦截器：401 自动刷新 ──
request.interceptors.response.use(
  (response) => response.data,
  async (error) => {
    const originalRequest = error.config;
    const status = error.response?.status;

    // 401 且非刷新接口 且未重试过 → 尝试刷新
    if (
      status === 401 &&
      originalRequest &&
      !originalRequest._retried &&
      !WHITE_LIST.some((p: string) => originalRequest.url.startsWith(p))
    ) {
      if (isRefreshing) {
        // 已有刷新在进行中，排队等待
        return new Promise((resolve, reject) => {
          pendingQueue.push((token: string) => {
            if (token) {
              originalRequest._retried = true;
              originalRequest.headers.Authorization = `Bearer ${token}`;
              resolve(request(originalRequest));
            } else {
              reject(error);
            }
          });
        });
      }

      const refreshToken = getRefreshToken();
      if (!refreshToken) {
        redirectToLogin();
        return Promise.reject(error);
      }

      isRefreshing = true;
      try {
        const res = await axios.post(
          `${import.meta.env.VITE_API_BASE_URL || ''}/api/auth/refresh`,
          { refreshToken },
        );
        const { accessToken, refreshToken: newRefresh } = res.data;
        setTokens(accessToken, newRefresh);

        // 重放排队请求
        pendingQueue.forEach((cb) => cb(accessToken));
        pendingQueue = [];

        // 重放原请求
        originalRequest._retried = true;
        originalRequest.headers.Authorization = `Bearer ${accessToken}`;
        return request(originalRequest);
      } catch (refreshError) {
        pendingQueue = [];
        redirectToLogin();
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    // 其他错误：全局提示
    const message = error.response?.data?.message || error.message || '请求失败';
    if (status !== 401) {
      Message.error(message);
    }
    return Promise.reject(error);
  },
);

function redirectToLogin() {
  removeTokens();
  localStorage.setItem('userStatus', 'logout');
  if (window.location.pathname !== '/login') {
    window.location.href = '/login';
  }
}

export default request;
```

- [ ] **Step 4: 创建认证 API 模块**

`frontend/src/api/auth.ts`：
```typescript
import request from './request';

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export interface CurrentUser {
  id: string;
  username: string;
  displayName: string;
  roles: string[];
  permissions: string[];
}

export function login(username: string, password: string) {
  return request.post<unknown, TokenResponse>('/api/auth/login', {
    username,
    password,
  });
}

export function refreshToken(refreshToken: string) {
  return request.post<unknown, TokenResponse>('/api/auth/refresh', {
    refreshToken,
  });
}

export function logout() {
  return request.post('/api/auth/logout');
}

export function getCurrentUser() {
  return request.get<unknown, CurrentUser>('/api/auth/me');
}
```

- [ ] **Step 5: 验证 TS 编译**

Run:
```bash
cd frontend && npx tsc --noEmit 2>&1 | head -20
```
Expected: 无报错（或仅有与认证无关的既有 warning）

- [ ] **Step 6: Commit**

```bash
git add frontend/.env.development frontend/.env.production frontend/src/utils/token.ts frontend/src/api/
git commit -m "feat(fe): axios 封装 + token 管理 + 认证 API 模块"
```

---

## Task 14: 前端 — 登录态与登录页改造

**Files:**
- Modify: `frontend/src/utils/checkLogin.tsx`
- Modify: `frontend/src/pages/login/form.tsx`
- Modify: `frontend/src/components/NavBar/index.tsx`

**Interfaces:**
- Consumes: token 工具（Task 13）、auth API（Task 13）
- Produces: 真实 token 驱动的登录态守卫、真实登录调用的登录页、真实登出的导航栏

- [ ] **Step 1: 改造 checkLogin.tsx**

将 `frontend/src/utils/checkLogin.tsx` 完整替换为：

```typescript
import { getAccessToken } from '@/utils/token';

export default function checkLogin() {
  return !!getAccessToken();
}
```

- [ ] **Step 2: 改造登录页 form.tsx**

修改 `frontend/src/pages/login/form.tsx`：

1) 将 `import axios from 'axios';` 替换为：

```typescript
import { login as loginApi } from '@/api/auth';
import { setTokens } from '@/utils/token';
```

2) 将 `afterLoginSuccess` 函数（第 29-40 行）中的登录记录逻辑替换：

```typescript
  function afterLoginSuccess(params) {
    // 记住密码
    if (rememberPassword) {
      setLoginParams(JSON.stringify(params));
    } else {
      removeLoginParams();
    }
    // 记录登录状态（与 checkLogin 兼容的过渡标志）
    localStorage.setItem('userStatus', 'login');
    // 跳转首页
    window.location.href = '/';
  }
```

3) 将 `login` 函数（第 42-58 行）替换为调用真实 API：

```typescript
  function login(params) {
    setErrorMessage('');
    setLoading(true);
    loginApi(params.userName, params.password)
      .then((res) => {
        setTokens(res.accessToken, res.refreshToken);
        afterLoginSuccess(params);
      })
      .catch((err) => {
        const msg = err.response?.data?.message || t['login.form.login.errMsg'];
        setErrorMessage(msg);
      })
      .finally(() => {
        setLoading(false);
      });
  }
```

4) 将表单初始密码默认值从 `'admin'` 改为空：

```typescript
        initialValues={{ userName: 'admin', password: '' }}
```

- [ ] **Step 3: 改造 NavBar 登出逻辑**

修改 `frontend/src/components/NavBar/index.tsx` 的 `logout` 函数（第 50-53 行）：

```typescript
  function logout() {
    logoutApi()
      .catch(() => {})
      .finally(() => {
        removeTokens();
        localStorage.setItem('userStatus', 'logout');
        window.location.href = '/login';
      });
  }
```

并在文件顶部 import 区追加：

```typescript
import { logout as logoutApi } from '@/api/auth';
import { removeTokens } from '@/utils/token';
```

- [ ] **Step 4: 验证 TS 编译**

Run:
```bash
cd frontend && npx tsc --noEmit 2>&1 | head -20
```
Expected: 无报错

- [ ] **Step 5: Commit**

```bash
git add frontend/src/utils/checkLogin.tsx frontend/src/pages/login/form.tsx frontend/src/components/NavBar/index.tsx
git commit -m "feat(fe): 登录页/登录态/登出改为真实 API + token 驱动"
```

---

## Task 15: 前端 — 用户信息获取与权限对接

**Files:**
- Modify: `frontend/src/main.tsx`
- Modify: `frontend/src/mock/user.ts`
- Modify: `frontend/src/store/index.ts` （可选，视对接需要）

**Interfaces:**
- Consumes: auth API `getCurrentUser`（Task 13）、token 工具
- Produces: 登录后拉取真实用户信息 + 权限，驱动菜单权限过滤

- [ ] **Step 1: 改造 main.tsx 的 fetchUserInfo**

修改 `frontend/src/main.tsx`：

1) 移除 `import axios from 'axios';`，替换为：

```typescript
import { getCurrentUser } from '@/api/auth';
```

2) 将 `fetchUserInfo` 函数（第 37-48 行）替换为：

```typescript
  function fetchUserInfo() {
    store.dispatch({
      type: 'update-userInfo',
      payload: { userLoading: true },
    });
    getCurrentUser()
      .then((user) => {
        // 后端 permissions 是 ["fabric:read", ...] 或 ["*"]
        // 前端 store 期望 Record<resource, actions[]>
        const permissions = transformPermissions(user.permissions, user.roles);
        store.dispatch({
          type: 'update-userInfo',
          payload: {
            userInfo: {
              name: user.displayName,
              permissions,
            },
            userLoading: false,
          },
        });
      })
      .catch(() => {
        store.dispatch({
          type: 'update-userInfo',
          payload: { userLoading: false },
        });
      });
  }
```

3) 在 `Index` 组件之前添加权限转换函数：

```typescript
/**
 * 把后端的 permCodes (["fabric:read", ...] 或 ["*"])
 * 转为前端 routes.ts 期望的 Record<resource, actions[]> 格式。
 * admin 角色返回 {"*": ["*"]} 的形式由 authentication.ts 的 * 通配处理。
 */
function transformPermissions(
  permCodes: string[],
  roles: string[],
): Record<string, string[]> {
  // admin 通配
  if (roles.includes('admin') || permCodes.includes('*')) {
    return { '*': ['*'] };
  }
  const result: Record<string, string[]> = {};
  permCodes.forEach((code) => {
    // code 格式: "资源:动作" 或 "模块:资源:动作"
    const parts = code.split(':');
    if (parts.length >= 2) {
      // 最后一段是 action，前面拼起来是 resource
      const action = parts[parts.length - 1];
      const resource = parts.slice(0, -1).join(':');
      if (!result[resource]) {
        result[resource] = [];
      }
      result[resource].push(action);
    }
  });
  return result;
}
```

> 注：现有 `routes.ts` 的权限 resource 如 `menu.dashboard.monitor`，与后端权限码 `fabric:read` 等**格式不同**。本轮 demo 菜单的 `requiredPermissions` 暂时不会精确匹配——但因为 admin 返回 `*` 通配（`authentication.ts` 中 `perm.join('') === '*'` 判 true），admin 用户能看到全部菜单，验证足够。精确的菜单-权限映射留待业务模块开发时调整。

- [ ] **Step 2: 移除 user mock 中的 login/userInfo 注册**

修改 `frontend/src/mock/user.ts`：删除整个文件内容中关于 `Mock.mock(new RegExp('/api/user/userInfo')` 和 `Mock.mock(new RegExp('/api/user/login')` 的两个注册块，仅保留文件结构（或直接清空 setup 函数体）。

将 `setup: () => { ... }` 内的 userInfo 和 login 两个 `Mock.mock(...)` 块删除。修改后 `setup` 应为空或仅含注释：

```typescript
import Mock from 'mockjs';
import { isSSR } from '@/utils/is';
import setupMock from '@/utils/setupMock';

if (!isSSR) {
  Mock.XHR.prototype.withCredentials = true;

  setupMock({
    setup: () => {
      // 登录与用户信息已改为真实后端接口，不再 mock。
      // message-box mock 仍保留在 message-box.ts。
    },
  });
}
```

- [ ] **Step 3: 验证 TS 编译**

Run:
```bash
cd frontend && npx tsc --noEmit 2>&1 | head -20
```
Expected: 无报错

- [ ] **Step 4: Commit**

```bash
git add frontend/src/main.tsx frontend/src/mock/user.ts
git commit -m "feat(fe): 用户信息改为真实 /api/auth/me + 权限转换 + 移除登录 mock"
```

---

## Task 16: 前端端到端验证

**Files:** 无（验证 Task 13-15 的产物）

**前提：** 后端正在运行（Task 12 已启动），数据库已迁移。

- [ ] **Step 1: 启动前端**

Run:
```bash
cd frontend && npm run dev
```
Expected: Vite 启动，监听 `http://localhost:5173`

- [ ] **Step 2: 验证登录跳转（未登录）**

浏览器打开 `http://localhost:5173`，应被重定向到 `/login`。
Expected: 跳转到登录页

- [ ] **Step 3: 验证真实登录**

在登录页输入 `admin` / `Admin@123`，点击登录。
Expected: 登录成功，跳转到首页，菜单正常渲染（admin 通配权限，应能看到 demo 菜单）。
检查 localStorage：`onecup_access_token` 与 `onecup_refresh_token` 有值。

- [ ] **Step 4: 验证刷新页面保持登录态**

在已登录状态下刷新浏览器（F5）。
Expected: 仍保持登录状态，不跳转到 `/login`（因为 `fetchUserInfo` 用 token 拉 `/api/auth/me` 成功）。

- [ ] **Step 5: 验证登出**

点击右上角头像下拉菜单的"退出登录"。
Expected: 调用 logout API，清除 token，跳转到 `/login`。

- [ ] **Step 6: 验证 token 过期自动刷新（手动）**

将后端 `appsettings.json` 的 `AccessTokenMinutes` 临时改为 `1`，重启后端，重新登录。等 1 分钟后操作页面（刷新或点击菜单），观察 Network：
Expected: 首次请求 401 → 自动调用 `/api/auth/refresh` → 用新 token 重放成功，用户无感知。

验证后改回 `AccessTokenMinutes: 30`。

- [ ] **Step 7: 最终全量提交（如有残留改动）**

```bash
git status
# 如有未提交的改动：
git add -A && git commit -m "chore: 认证全链路验证通过"
```

---

## Self-Review 记录

完成全部任务编写后的自查：

1. **Spec 覆盖：**
   - 数据模型 6 张表 → Task 1(实体) + Task 3(配置) + Task 11(迁移) ✅
   - 后端 4 端点 → Task 10 ✅
   - JWT 签发/校验 → Task 7(签发) + Task 9(校验中间件) ✅
   - 密码 BCrypt → Task 4(实现) + Task 6(测试) ✅
   - 种子数据 → Task 5 ✅
   - 前端 axios 封装 → Task 13 ✅
   - 登录态改造 → Task 14 ✅
   - 权限对接 → Task 15 ✅
   - Refresh token 机制 → Task 7(生成) + Task 8(轮换) + Task 13(前端自动刷新) ✅
   - 验证标准 9 条 → Task 12(后端 7 条) + Task 16(前端 2 条) ✅

2. **占位符：** admin 密码哈希在 Task 4 预算 → Task 5 直接填入，无占位符提交 ✅

3. **类型一致性：**
   - `TokenResponse` 的 `accessToken`/`refreshToken`/`expiresIn` 在后端 DTO、AuthService、前端接口定义一致 ✅
   - `CurrentUser` 的 `roles`/`permissions` 在后端 DTO、AuthService、前端接口一致 ✅
   - `IAuthService` 四方法签名与 AuthController 调用、AuthService 实现一致 ✅

4. **潜在风险点（实现时注意）：**
   - EF Core 多对多 `UsingEntity` 的 `HasData` 语法（Task 5 Step 1 的 DbContext Seed 方法）需验证 `HasData` 对 join 表的写法——若报错，调整为直接对 join entity 配置 `HasData`。这是 EF Core 10 API 的已知细节，实现时按编译反馈调整。
   - `ClaimTypes.NameIdentifier` 与 JWT `Sub` claim 的映射——JwtBearer 默认将 `sub` 映射到 `ClaimTypes.NameIdentifier`，`CurrentUserService` 据此取 userId。
