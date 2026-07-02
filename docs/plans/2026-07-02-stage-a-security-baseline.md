# 阶段 A:后端安全基线 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 OneCup 后端建立可上线安全基线:JWT 密钥启动校验、登录/刷新限流与失败锁定、安全事件日志、生产环境异常不回显、admin 通配逻辑收敛。

**Architecture:** 在现有四层 Clean Architecture(`Api → Infrastructure → Application → Domain`)内增量加固。新增 `IPermissionCalculator`(Application 层)收敛通配逻辑;新增 `ILockoutStore`(Infrastructure 层)抽象失败锁定;在 `Program.cs` 配置 .NET 内置 `RateLimiter`;全局异常处理器区分环境并补日志。所有改动保持现有接口签名稳定(`IAuthService` 不变),锁定状态通过新异常 `AccountLockedException` 表达。

**Tech Stack:** .NET 10 / ASP.NET Core / EF Core / `System.Threading.RateLimiting`(内置)/ `Microsoft.Extensions.Caching.Memory`(Infrastructure 需加包)/ xUnit + InMemory + 手写 fake。

## Global Constraints

- 目标框架 `net10.0`;`Nullable` 与 `ImplicitUsings` 已开启。
- 测试风格:xUnit `[Fact]`、EF Core `UseInMemoryDatabase`(每用例唯一 db 名)、**手写 sealed fake 类**(项目无 Moq/NSubstitute,沿用此风格)。
- `IAuthService` 接口签名**保持不变**(锁定通过异常表达,不改变返回类型)。
- 日志不记录密码、完整 token;refresh token 日志只记掩码后前 8 字符。
- 限流/锁定用**内存方案**,接口抽象 `ILockoutStore` 预留 Redis 替换点(spec 9.2)。
- 占位符密钥常量 `"REPLACE_VIA_USER_SECRETS"`(27 字符,见 `appsettings.json:27`)。
- HMAC-SHA256 要求密钥 ≥ 32 字节(256 bit)。
- 现有 `IRepository<T>` 定义在 `backend/src/OneCup.Application/Interfaces/IUnitOfWork.cs:17`(无独立 IRepository.cs)。
- `OneCup.Infrastructure` 是普通 SDK(非 Web),用 `IMemoryCache` 需显式加包;`OneCup.Api` 是 Web SDK,缓存/限流开箱即用。

---

## File Structure

| 文件 | 责任 | 动作 |
|------|------|------|
| `backend/src/OneCup.Application/Options/JwtOptions.cs` | 加占位符常量 | Modify |
| `backend/src/OneCup.Application/Validators/JwtOptionsValidator.cs` | 启动校验密钥 | Create |
| `backend/src/OneCup.Application/Interfaces/IPermissionCalculator.cs` | 通配/权限聚合单一来源 | Create |
| `backend/src/OneCup.Application/Services/PermissionCalculator.cs` | 实现 | Create |
| `backend/src/OneCup.Domain/Exceptions/AccountLockedException.cs` | 锁定异常 | Create |
| `backend/src/OneCup.Infrastructure/Interfaces/ILockoutStore.cs` | 锁定存储抽象 | Create |
| `backend/src/OneCup.Infrastructure/Lockout/MemoryLockoutStore.cs` | 内存实现 | Create |
| `backend/src/OneCup.Infrastructure/Services/AuthService.cs` | 加锁定+日志 | Modify |
| `backend/src/OneCup.Infrastructure/Services/JwtTokenService.cs` | 用 IPermissionCalculator | Modify |
| `backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj` | 加缓存包 | Modify |
| `backend/src/OneCup.Api/Authorization/WildcardAuthorizationHandler.cs` | 用 IPermissionCalculator | Modify |
| `backend/src/OneCup.Api/Program.cs` | 注册校验器/限流/锁定/日志中间件 | Modify |
| 测试文件(见各 Task) | — | Create/Modify |

---

## Task 1: JWT SecretKey 启动校验(fail-fast)

**Files:**
- Modify: `backend/src/OneCup.Application/Options/JwtOptions.cs`
- Create: `backend/src/OneCup.Application/Validators/JwtOptionsValidator.cs`
- Test: `backend/tests/OneCup.UnitTests/Options/JwtOptionsValidatorTests.cs`

**Interfaces:**
- Produces: `JwtOptions.PlaceholderSecret`(常量 `"REPLACE_VIA_USER_SECRETS"`)、`ValidateOptionsResult JwtOptionsValidator.Validate(JwtOptions)`。

- [ ] **Step 1: 写失败测试**

```csharp
// backend/tests/OneCup.UnitTests/Options/JwtOptionsValidatorTests.cs
using Microsoft.Extensions.Options;
using OneCup.Application.Options;
using OneCup.Application.Validators;

namespace OneCup.UnitTests.Options;

public class JwtOptionsValidatorTests
{
    private readonly JwtOptionsValidator _validator = new();

    [Fact]
    public void Validate_null_or_empty_secret_fails()
    {
        var options = new JwtOptions { SecretKey = "", Issuer = "x", Audience = "x" };
        var result = _validator.Validate(null, options);
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_placeholder_secret_fails()
    {
        var options = new JwtOptions { SecretKey = JwtOptions.PlaceholderSecret, Issuer = "x", Audience = "x" };
        var result = _validator.Validate(null, options);
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_short_secret_under_32_bytes_fails()
    {
        var options = new JwtOptions { SecretKey = "short-key-only-20-chars", Issuer = "x", Audience = "x" };
        var result = _validator.Validate(null, options);
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_secret_at_least_32_bytes_succeeds()
    {
        var options = new JwtOptions { SecretKey = "this-is-a-valid-secret-key-32+bytes!", Issuer = "x", Audience = "x" };
        var result = _validator.Validate(null, options);
        Assert.True(result.Succeeded);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~JwtOptionsValidatorTests"`
Expected: FAIL — 类型 `JwtOptionsValidator` 不存在。

- [ ] **Step 3: 加占位符常量到 JwtOptions**

在 `backend/src/OneCup.Application/Options/JwtOptions.cs` 的 `SectionName` 常量下方加:

```csharp
    /// <summary>未配置时 appsettings 的占位符。启动校验拒绝该值。</summary>
    public const string PlaceholderSecret = "REPLACE_VIA_USER_SECRETS";
```

- [ ] **Step 4: 实现 JwtOptionsValidator**

```csharp
// backend/src/OneCup.Application/Validators/JwtOptionsValidator.cs
using Microsoft.Extensions.Options;
using OneCup.Application.Options;

namespace OneCup.Application.Validators;

/// <summary>
/// 启动时校验 JWT 配置:SecretKey 必须非空、非占位符、≥32 字节(HS256 要求)。
/// </summary>
public class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            return ValidateOptionsResult.Fail("Jwt:SecretKey 未配置。请通过 user-secrets 或环境变量提供。");
        }

        if (options.SecretKey == JwtOptions.PlaceholderSecret)
        {
            return ValidateOptionsResult.Fail("Jwt:SecretKey 仍为占位符,请通过 user-secrets 或环境变量配置真实密钥。");
        }

        // UTF-8 字节数(HS256 要求 ≥256 bit = 32 字节)
        if (System.Text.Encoding.UTF8.GetByteCount(options.SecretKey) < 32)
        {
            return ValidateOptionsResult.Fail("Jwt:SecretKey 长度不足,HS256 要求至少 32 字节(256 bit)。");
        }

        return ValidateOptionsResult.Success;
    }
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~JwtOptionsValidatorTests"`
Expected: PASS(4 个用例)。

- [ ] **Step 6: 在 Program.cs 注册校验器(ValidateOnStart)**

在 `Program.cs` 现有 `builder.Services.Configure<JwtOptions>(...)`(第 49 行)之后加:

```csharp
builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
builder.Services.AddOptions<JwtOptions>().ValidateOnStart();
```
并在文件顶部 using 区加 `using OneCup.Application.Validators;`。

- [ ] **Step 7: 构建验证**

Run: `dotnet build backend/src/OneCup.Api/OneCup.Api.csproj`
Expected: 构建成功。

- [ ] **Step 8: 提交**

```bash
git add backend/src/OneCup.Application/Options/JwtOptions.cs backend/src/OneCup.Application/Validators/ backend/tests/OneCup.UnitTests/Options/ backend/src/OneCup.Api/Program.cs
git commit -m "feat(sec): JWT SecretKey 启动校验 (fail-fast)
```

---

## Task 2: IPermissionCalculator 收敛 admin 通配逻辑

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/IPermissionCalculator.cs`
- Create: `backend/src/OneCup.Application/Services/PermissionCalculator.cs`
- Test: `backend/tests/OneCup.UnitTests/Auth/PermissionCalculatorTests.cs`

**Interfaces:**
- Produces: `bool IPermissionCalculator.IsWildcard(IReadOnlyCollection<string> permCodes)`、`IReadOnlyList<string> IPermissionCalculator.GetEffective(User user)`。`GetEffective` 接收 `User`(含 `.Roles`→`.Permissions`),admin 角色(`Roles.Any(r => r.Code == "admin")`)返回 `["*"]`,否则聚合去重权限 code。

- [ ] **Step 1: 写失败测试**

```csharp
// backend/tests/OneCup.UnitTests/Auth/PermissionCalculatorTests.cs
using OneCup.Application.Services;
using OneCup.Domain.Entities;

namespace OneCup.UnitTests.Auth;

public class PermissionCalculatorTests
{
    private readonly PermissionCalculator _calc = new();

    [Fact]
    public void IsWildcard_true_when_contains_star()
    {
        Assert.True(_calc.IsWildcard(new[] { "fabric:read", "*" }));
    }

    [Fact]
    public void IsWildcard_false_without_star()
    {
        Assert.False(_calc.IsWildcard(new[] { "fabric:read" }));
    }

    [Fact]
    public void GetEffective_admin_role_returns_wildcard()
    {
        var adminRole = new Role { Code = "admin", Permissions = new List<Permission>() };
        var user = new User { Roles = new List<Role> { adminRole } };
        var result = _calc.GetEffective(user);
        Assert.Equal(new[] { "*" }, result);
    }

    [Fact]
    public void GetEffective_non_admin_aggregates_and_dedupes()
    {
        var dev = new Role
        {
            Code = "developer",
            Permissions = new List<Permission>
            {
                new() { Code = "fabric:read" },
                new() { Code = "material:read" },
            }
        };
        var other = new Role
        {
            Code = "viewer",
            Permissions = new List<Permission>
            {
                new() { Code = "fabric:read" },          // 重复
                new() { Code = "system:user:manage" },
            }
        };
        var user = new User { Roles = new List<Role> { dev, other } };
        var result = _calc.GetEffective(user);
        Assert.Equal(new[] { "fabric:read", "material:read", "system:user:manage" }, result.OrderBy(x => x));
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~PermissionCalculatorTests"`
Expected: FAIL — 类型不存在。

- [ ] **Step 3: 定义接口**

```csharp
// backend/src/OneCup.Application/Interfaces/IPermissionCalculator.cs
using OneCup.Domain.Entities;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 权限计算的单一来源:admin 通配判断、角色权限聚合。
/// 消除 AuthService / JwtTokenService / WildcardHandler 三处重复。
/// </summary>
public interface IPermissionCalculator
{
    /// <summary>权限编码集合是否含通配 "*"。</summary>
    bool IsWildcard(IReadOnlyCollection<string> permCodes);

    /// <summary>
    /// 计算用户的生效权限编码:含 admin 角色返回 ["*"],
    /// 否则聚合所有角色的权限并去重。
    /// </summary>
    IReadOnlyList<string> GetEffective(User user);
}
```

- [ ] **Step 4: 实现 PermissionCalculator**

```csharp
// backend/src/OneCup.Application/Services/PermissionCalculator.cs
using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;

namespace OneCup.Application.Services;

public class PermissionCalculator : IPermissionCalculator
{
    private const string AdminRoleCode = "admin";
    private const string Wildcard = "*";

    public bool IsWildcard(IReadOnlyCollection<string> permCodes) => permCodes.Contains(Wildcard);

    public IReadOnlyList<string> GetEffective(User user)
    {
        if (user.Roles.Any(r => r.Code == AdminRoleCode))
        {
            return new List<string> { Wildcard };
        }

        return user.Roles
            .SelectMany(r => r.Permissions)
            .Select(p => p.Code)
            .Distinct()
            .ToList();
    }
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~PermissionCalculatorTests"`
Expected: PASS(4 个用例)。

- [ ] **Step 6: 提交**

```bash
git add backend/src/OneCup.Application/Interfaces/IPermissionCalculator.cs backend/src/OneCup.Application/Services/PermissionCalculator.cs backend/tests/OneCup.UnitTests/Auth/PermissionCalculatorTests.cs
git commit -m "feat(sec): IPermissionCalculator 收敛 admin 通配逻辑"
```

---

## Task 3: 接入 IPermissionCalculator(JwtTokenService / AuthService / WildcardHandler)

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/Services/JwtTokenService.cs:13-27`
- Modify: `backend/src/OneCup.Infrastructure/Services/AuthService.cs:77-97`
- Modify: `backend/src/OneCup.Api/Authorization/WildcardAuthorizationHandler.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`(注册)
- Modify: `backend/tests/OneCup.UnitTests/Auth/AuthServiceTests.cs:55-59`(构造函数新增依赖)
- Modify: `backend/tests/OneCup.UnitTests/Auth/JwtTokenServiceTests.cs`(构造函数)

**Interfaces:**
- Consumes: Task 2 的 `IPermissionCalculator`(实现 `PermissionCalculator`)。
- Produces: `JwtTokenService` 构造函数增加 `IPermissionCalculator` 参数;`AuthService` 同;`WildcardAuthorizationHandler` 注入 `IPermissionCalculator`(替换 `HasClaim("perm_codes","*")` 直接判断,改用 `IsWildcard` 读 claims)。

> 注意:`WildcardAuthorizationHandler` 是 AuthorizationHandler,从 `context.User.FindAll("perm_codes")` 取 claim 值集合,交给 `IsWildcard` 判断。这把"什么算通配"的语义统一到一处。

- [ ] **Step 1: 改造 JwtTokenService**

修改 `JwtTokenService.cs`:
- 字段加 `private readonly IPermissionCalculator _permCalc;`
- 构造函数:`public JwtTokenService(IOptions<JwtOptions> options, IPermissionCalculator permCalc)`,赋值 `_permCalc = permCalc;`、`_options = options.Value;`
- `GenerateAccessToken` 中第 23-27 行替换为:

```csharp
        var roleCodes = user.Roles.Select(r => r.Code).ToList();
        var permCodes = _permCalc.GetEffective(user);
```
- 文件顶部 using 加 `using OneCup.Application.Services;`(若 PermissionCalculator 命名空间未被 using 覆盖;实际 `IPermissionCalculator` 在 `OneCup.Application.Interfaces`,已有 using)。构造函数参数类型用接口 `IPermissionCalculator`,加 using `OneCup.Application.Interfaces`(已有)。

- [ ] **Step 2: 改造 AuthService.GetCurrentUserAsync**

修改 `AuthService.cs` 第 77-97 行,把第 84-87 行替换为:

```csharp
        var roleCodes = user.Roles.Select(r => r.Code).ToList();
        var permCodes = _permCalc.GetEffective(user);
```
并在构造函数加 `IPermissionCalculator permCalc` 参数与 `_permCalc = permCalc;` 字段。字段声明区加 `private readonly IPermissionCalculator _permCalc;`。

- [ ] **Step 3: 改造 WildcardAuthorizationHandler**

```csharp
// backend/src/OneCup.Api/Authorization/WildcardAuthorizationHandler.cs
using Microsoft.AspNetCore.Authorization;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Authorization;

/// <summary>
/// 通配授权:用户 perm_codes 含 "*" 时放行所有策略。
/// 通配语义委托 IPermissionCalculator,保持单一来源。
/// </summary>
public class WildcardAuthorizationHandler : AuthorizationHandler<IAuthorizationRequirement>
{
    private readonly IPermissionCalculator _permCalc;

    public WildcardAuthorizationHandler(IPermissionCalculator permCalc)
    {
        _permCalc = permCalc;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, IAuthorizationRequirement requirement)
    {
        var permClaims = context.User.FindAll("perm_codes").Select(c => c.Value).ToList();
        if (_permCalc.IsWildcard(permClaims))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: 注册 IPermissionCalculator**

在 `Program.cs` 注册区(第 77 行附近,Service 注册块)加:

```csharp
builder.Services.AddSingleton<IPermissionCalculator, PermissionCalculator>();
```
顶部 using 加 `using OneCup.Application.Services;`。

- [ ] **Step 5: 修复现有测试的构造函数调用**

`AuthServiceTests.cs:55-59` 的 `CreateAuthService`:

```csharp
    private AuthService CreateAuthService(OneCupDbContext db, FakePasswordHasher? passwordHasher = null, FakeJwtTokenService? jwt = null)
    {
        passwordHasher ??= new FakePasswordHasher();
        jwt ??= new FakeJwtTokenService();
        var permCalc = new PermissionCalculator();
        return new AuthService(db, jwt, passwordHasher, Options.Create(_options), permCalc);
    }
```
顶部 using 加 `using OneCup.Application.Services;`。

`JwtTokenServiceTests.cs` 中所有 `new JwtTokenService(Options.Create(options))` 调用改为 `new JwtTokenService(Options.Create(options), new PermissionCalculator())`。顶部 using 加 `using OneCup.Application.Services;`。

- [ ] **Step 6: 运行全部后端测试**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: PASS(所有现有用例 + 新 PermissionCalculator 用例)。

- [ ] **Step 7: 提交**

```bash
git add backend/src/OneCup.Infrastructure/Services/JwtTokenService.cs backend/src/OneCup.Infrastructure/Services/AuthService.cs backend/src/OneCup.Api/Authorization/WildcardAuthorizationHandler.cs backend/src/OneCup.Api/Program.cs backend/tests/OneCup.UnitTests/Auth/AuthServiceTests.cs backend/tests/OneCup.UnitTests/Auth/JwtTokenServiceTests.cs
git commit -m "refactor(sec): 三处 admin 通配逻辑接入 IPermissionCalculator"
```

---

## Task 4: AccountLockedException + ILockoutStore(失败锁定抽象)

**Files:**
- Create: `backend/src/OneCup.Domain/Exceptions/AccountLockedException.cs`
- Create: `backend/src/OneCup.Infrastructure/Interfaces/ILockoutStore.cs`
- Create: `backend/src/OneCup.Infrastructure/Lockout/MemoryLockoutStore.cs`
- Modify: `backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj`(加缓存包)
- Test: `backend/tests/OneCup.UnitTests/Lockout/MemoryLockoutStoreTests.cs`

**Interfaces:**
- Produces:
  - `AccountLockedException : UnauthorizedException`(新增 `RetryAfter` 属性,TimeSpan)
  - `ILockoutStore`: `Task<bool> IsLockedAsync(string key, ct)`、`Task RecordFailureAsync(string key, ct)`、`Task ResetAsync(string key, ct)`、`Task<TimeSpan?> GetRemainingLockoutAsync(string key, ct)`
  - `MemoryLockoutStore(IMemoryCache, IOptions<LockoutOptions>)`:阈值 5 次 / 锁定 15 分钟

- [ ] **Step 1: 加缓存包引用**

修改 `backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj`,在 `<ItemGroup>` 加:

```xml
    <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="10.0.0" />
```

Run: `cd backend/src/OneCup.Infrastructure && dotnet restore`

- [ ] **Step 2: 定义 LockoutOptions**

```csharp
// backend/src/OneCup.Application/Options/LockoutOptions.cs
namespace OneCup.Application.Options;

/// <summary>失败锁定参数。内存方案,多实例需换 Redis(见 spec 9.2)。</summary>
public class LockoutOptions
{
    public const string SectionName = "Lockout";

    /// <summary>连续失败多少次后锁定。默认 5。</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>锁定时长。默认 15 分钟。</summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}
```

- [ ] **Step 3: 写失败测试**

```csharp
// backend/tests/OneCup.UnitTests/Lockout/MemoryLockoutStoreTests.cs
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OneCup.Application.Options;
using OneCup.Infrastructure.Lockout;

namespace OneCup.UnitTests.Lockout;

public class MemoryLockoutStoreTests
{
    private static MemoryLockoutStore CreateStore(int maxAttempts = 5, int lockMin = 15)
    {
        var options = Options.Create(new LockoutOptions
        {
            MaxFailedAttempts = maxAttempts,
            LockoutDuration = TimeSpan.FromMinutes(lockMin),
        });
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new MemoryLockoutStore(cache, options);
    }

    [Fact]
    public async Task Not_locked_when_under_threshold()
    {
        var store = CreateStore(maxAttempts: 3);
        await store.RecordFailureAsync("u1", default);
        await store.RecordFailureAsync("u1", default);
        Assert.False(await store.IsLockedAsync("u1", default));
    }

    [Fact]
    public async Task Locked_after_threshold_failures()
    {
        var store = CreateStore(maxAttempts: 3);
        for (var i = 0; i < 3; i++) await store.RecordFailureAsync("u1", default);
        Assert.True(await store.IsLockedAsync("u1", default));
    }

    [Fact]
    public async Task Reset_clears_failures()
    {
        var store = CreateStore(maxAttempts: 3);
        for (var i = 0; i < 3; i++) await store.RecordFailureAsync("u1", default);
        await store.ResetAsync("u1", default);
        Assert.False(await store.IsLockedAsync("u1", default));
    }

    [Fact]
    public async Task Remaining_lockout_positive_when_locked()
    {
        var store = CreateStore(maxAttempts: 1, lockMin: 15);
        await store.RecordFailureAsync("u1", default);
        var remaining = await store.GetRemainingLockoutAsync("u1", default);
        Assert.NotNull(remaining);
        Assert.True(remaining > TimeSpan.Zero && remaining <= TimeSpan.FromMinutes(15));
    }
}
```

- [ ] **Step 4: 运行测试验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~MemoryLockoutStoreTests"`
Expected: FAIL — 类型不存在。

- [ ] **Step 5: 定义 AccountLockedException**

```csharp
// backend/src/OneCup.Domain/Exceptions/AccountLockedException.cs
namespace OneCup.Domain.Exceptions;

/// <summary>账号因连续登录失败被锁定。</summary>
public class AccountLockedException : UnauthorizedException
{
    /// <summary>剩余锁定时长。</summary>
    public TimeSpan? RetryAfter { get; }

    public AccountLockedException(TimeSpan? retryAfter)
        : base("账号已被锁定,请稍后再试")
    {
        RetryAfter = retryAfter;
    }
}
```

确认 `UnauthorizedException` 构造函数签名兼容(读 `backend/src/OneCup.Domain/Exceptions/UnauthorizedException.cs` 应有 `(string message)` 构造)。如签名不同则适配。

- [ ] **Step 6: 定义 ILockoutStore + MemoryLockoutStore**

```csharp
// backend/src/OneCup.Infrastructure/Interfaces/ILockoutStore.cs
namespace OneCup.Infrastructure.Interfaces;

/// <summary>
/// 失败锁定存储抽象。当前为内存实现,多实例部署替换为 Redis(见 spec 9.2)。
/// </summary>
public interface ILockoutStore
{
    Task<bool> IsLockedAsync(string key, CancellationToken ct = default);
    Task RecordFailureAsync(string key, CancellationToken ct = default);
    Task ResetAsync(string key, CancellationToken ct = default);
    Task<TimeSpan?> GetRemainingLockoutAsync(string key, CancellationToken ct = default);
}
```

```csharp
// backend/src/OneCup.Infrastructure/Lockout/MemoryLockoutStore.cs
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OneCup.Application.Options;
using OneCup.Infrastructure.Interfaces;

namespace OneCup.Infrastructure.Lockout;

/// <summary>
/// 基于 IMemoryCache 的失败锁定存储。单实例可用。
/// 计数 key:lockout:fail:{key}; 锁定 key:lockout:until:{key}。
/// </summary>
public class MemoryLockoutStore : ILockoutStore
{
    private readonly IMemoryCache _cache;
    private readonly LockoutOptions _options;

    public MemoryLockoutStore(IMemoryCache cache, IOptions<LockoutOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public Task<bool> IsLockedAsync(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue< DateTimeOffset>(LockKey(key), out var until))
        {
            return Task.FromResult(until > DateTimeOffset.UtcNow);
        }
        return Task.FromResult(false);
    }

    public Task RecordFailureAsync(string key, CancellationToken ct = default)
    {
        var failKey = FailKey(key);
        var attempts = _cache.GetOrCreate(failKey, e =>
        {
            e.AbsoluteExpirationRelativeToNow = _options.LockoutDuration;
            return 0;
        }) + 1;

        if (attempts >= _options.MaxFailedAttempts)
        {
            var lockedUntil = DateTimeOffset.UtcNow.Add(_options.LockoutDuration);
            _cache.Set(LockKey(key), lockedUntil, _options.LockoutDuration);
        }
        else
        {
            _cache.Set(failKey, attempts, _options.LockoutDuration);
        }
        return Task.CompletedTask;
    }

    public Task ResetAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(FailKey(key));
        _cache.Remove(LockKey(key));
        return Task.CompletedTask;
    }

    public Task<TimeSpan?> GetRemainingLockoutAsync(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue<DateTimeOffset>(LockKey(key), out var until))
        {
            var remaining = until - DateTimeOffset.UtcNow;
            return Task.FromResult<TimeSpan?>(remaining > TimeSpan.Zero ? remaining : null);
        }
        return Task.FromResult<TimeSpan?>(null);
    }

    private static string FailKey(string key) => $"lockout:fail:{key}";
    private static string LockKey(string key) => $"lockout:until:{key}";
}
```

- [ ] **Step 7: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~MemoryLockoutStoreTests"`
Expected: PASS(4 个用例)。

- [ ] **Step 8: 提交**

```bash
git add backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj backend/src/OneCup.Application/Options/LockoutOptions.cs backend/src/OneCup.Domain/Exceptions/AccountLockedException.cs backend/src/OneCup.Infrastructure/Interfaces/ILockoutStore.cs backend/src/OneCup.Infrastructure/Lockout/MemoryLockoutStore.cs backend/tests/OneCup.UnitTests/Lockout/
git commit -m "feat(sec): AccountLockedException + ILockoutStore 失败锁定抽象(内存实现)"
```

---

## Task 5: AuthService 接入失败锁定 + 安全日志

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/Services/AuthService.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`(注册 ILockoutStore / IMemoryCache / LockoutOptions 绑定)
- Test: `backend/tests/OneCup.UnitTests/Auth/AuthServiceTests.cs`

**Interfaces:**
- Consumes: Task 4 的 `ILockoutStore`、`AccountLockedException`。
- Produces: `AuthService` 构造函数新增 `ILockoutStore lockout`、`ILogger<AuthService> logger`。`LoginAsync` 流程:先查锁定→锁定中抛 `AccountLockedException`→密码校验失败时 `RecordFailureAsync` 并记日志→成功 `ResetAsync` 并记日志。

- [ ] **Step 1: 先写失败测试(锁定场景)**

在 `AuthServiceTests.cs` 新增用例(放现有失败用例之后):

```csharp
    private static ILockoutStore NoLockStore() => new FakeLockoutStore();

    [Fact]
    public async Task Login_locked_account_throws_AccountLockedException()
    {
        // 安排:存储报告该用户已锁定
        var lockedStore = new FakeLockoutStore { IsLockedResult = true, Remaining = TimeSpan.FromMinutes(10) };
        var db = await CreateContextAsync(nameof(Login_locked_account_throws_AccountLockedException));
        var sut = CreateAuthService(db, lockout: lockedStore);

        // 断言
        var ex = await Assert.ThrowsAsync<AccountLockedException>(
            () => sut.LoginAsync(new LoginRequest("admin", "any"), default));
        Assert.NotNull(ex.RetryAfter);
    }

    [Fact]
    public async Task Login_wrong_password_records_failure()
    {
        var store = new FakeLockoutStore();
        var db = await CreateContextAsync(nameof(Login_wrong_password_records_failure));
        var sut = CreateAuthService(db,
            passwordHasher: new FakePasswordHasher { VerifyResult = (_, _) => false },
            lockout: store);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => sut.LoginAsync(new LoginRequest("admin", "wrong"), default));
        Assert.Equal(1, store.FailureCount);
    }

    [Fact]
    public async Task Login_success_resets_failures()
    {
        var store = new FakeLockoutStore { FailureCount = 3 };
        var db = await CreateContextAsync(nameof(Login_success_resets_failures));
        var sut = CreateAuthService(db, lockout: store);

        await sut.LoginAsync(new LoginRequest("admin", "pass"), default);
        Assert.True(store.WasReset);
    }
```

并在文件底部加 fake:

```csharp
    /// <summary>可控的锁定存储 fake。</summary>
    private sealed class FakeLockoutStore : ILockoutStore
    {
        public bool IsLockedResult { get; set; }
        public TimeSpan? Remaining { get; set; }
        public int FailureCount { get; set; }
        public bool WasReset { get; set; }

        public Task<bool> IsLockedAsync(string key, CancellationToken ct = default) => Task.FromResult(IsLockedResult);
        public Task RecordFailureAsync(string key, CancellationToken ct = default) { FailureCount++; return Task.CompletedTask; }
        public Task ResetAsync(string key, CancellationToken ct = default) { WasReset = true; return Task.CompletedTask; }
        public Task<TimeSpan?> GetRemainingLockoutAsync(string key, CancellationToken ct = default) => Task.FromResult(Remaining);
    }
```

修改 `CreateAuthService` 签名加 `lockout` 参数:

```csharp
    private AuthService CreateAuthService(OneCupDbContext db, FakePasswordHasher? passwordHasher = null,
        FakeJwtTokenService? jwt = null, ILockoutStore? lockout = null)
    {
        passwordHasher ??= new FakePasswordHasher();
        jwt ??= new FakeJwtTokenService();
        lockout ??= new FakeLockoutStore();
        var permCalc = new PermissionCalculator();
        var logger = NullLogger<AuthService>.Instance;
        return new AuthService(db, jwt, passwordHasher, Options.Create(_options), permCalc, lockout, logger);
    }
```
顶部 using 加:`using Microsoft.Extensions.Logging;`、`using OneCup.Domain.Exceptions;`、`using OneCup.Infrastructure.Interfaces;`。

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~AuthServiceTests"`
Expected: FAIL — `AuthService` 构造函数缺少 lockout/logger 参数。

- [ ] **Step 3: 改造 AuthService 构造函数 + LoginAsync**

在 `AuthService.cs`:
- 字段加 `private readonly ILockoutStore _lockout;`、`private readonly ILogger<AuthService> _logger;`
- 构造函数追加两参数并赋值。
- 顶部 using 加 `using Microsoft.Extensions.Logging;`、`using OneCup.Domain.Exceptions;`、`using OneCup.Infrastructure.Interfaces;`。
- 替换 `LoginAsync`(第 32-44 行)为:

```csharp
    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var lockoutKey = request.Username.ToLowerInvariant();

        // 1. 先查锁定(不查库、不校验密码)
        if (await _lockout.IsLockedAsync(lockoutKey, ct))
        {
            var remaining = await _lockout.GetRemainingLockoutAsync(lockoutKey, ct);
            _logger.LogWarning("登录被拒(账号锁定):Username={Username}, 剩余={Remaining}", request.Username, remaining);
            throw new AccountLockedException(remaining);
        }

        // 2. 查用户
        var user = await _db.Users
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Username == request.Username, ct);

        if (user is null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            // 失败:记录 + 计数(不泄露用户是否存在)
            await _lockout.RecordFailureAsync(lockoutKey, ct);
            _logger.LogWarning("登录失败:Username={Username}", request.Username);
            throw new UnauthorizedException("用户名或密码错误");
        }

        // 3. 成功:重置计数 + 日志
        await _lockout.ResetAsync(lockoutKey, ct);
        _logger.LogInformation("登录成功:UserId={UserId}, Username={Username}", user.Id, user.Username);

        return await IssueTokensAsync(user, ct);
    }
```

- [ ] **Step 4: 在 Program.cs 注册锁定依赖**

在 Service 注册块加:

```csharp
builder.Services.AddMemoryCache();
builder.Services.Configure<LockoutOptions>(builder.Configuration.GetSection(LockoutOptions.SectionName));
builder.Services.AddSingleton<ILockoutStore, MemoryLockoutStore>();
```
顶部 using 加 `using OneCup.Application.Options;`(已有)、`using OneCup.Infrastructure.Lockout;`、`using OneCup.Infrastructure.Interfaces;`。
并在 `appsettings.json` 的 `Jwt` 节后加(可选,用默认值即可):

```json
  "Lockout": {
    "MaxFailedAttempts": 5,
    "LockoutDuration": "00:15:00"
  }
```

- [ ] **Step 5: 运行全部测试**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: PASS(含新增 3 个锁定用例 + 现有用例)。

- [ ] **Step 6: 提交**

```bash
git add backend/src/OneCup.Infrastructure/Services/AuthService.cs backend/src/OneCup.Api/Program.cs backend/src/OneCup.Api/appsettings.json backend/tests/OneCup.UnitTests/Auth/AuthServiceTests.cs
git commit -m "feat(sec): AuthService 登录失败锁定 + 安全事件日志"
```

---

## Task 6: 登录/刷新端点限流

**Files:**
- Modify: `backend/src/OneCup.Api/Program.cs`(AddRateLimiter + UseRateLimiter)
- Modify: `backend/src/OneCup.Api/Controllers/AuthController.cs`(标注 `[EnableRateLimiting]`)

**Interfaces:**
- Produces:命名限流策略 `"auth-login"`(固定窗口,按 IP,permitLimit 10/window 1min)与全局 `"global"` 兜底;`/api/auth/login` 与 `/api/auth/refresh` 标注 `"auth-login"`。

> .NET 内置 `System.Threading.RateLimiting` 与 `AddRateLimiter` 属 `Microsoft.AspNetCore.App` 共享框架,Api 项目无需加包。

- [ ] **Step 1: 在 Program.cs 配置限流**

在 `var app = builder.Build();`(第 91 行)**之前**加:

```csharp
// ── 限流 ──
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 登录/刷新:按 IP 固定窗口,10 次/分钟
    options.AddFixedWindowLimiter("auth-login", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // 全局兜底:按 IP,120 次/分钟
    options.AddFixedWindowLimiter("global", opt =>
    {
        opt.PermitLimit = 120;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

    options.GlobalLimiter = System.Threading.RateLimiting.Partitioned.CreateRateLimiter<Microsoft.AspNetCore.Http.HttpContext>(ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });
});
```

- [ ] **Step 2: 启用限流中间件**

在 `Program.cs` 的 `app.UseAuthentication();`(第 101 行)**之前**加:

```csharp
app.UseRateLimiter();
```

- [ ] **Step 3: 给 AuthController 标注限流**

修改 `backend/src/OneCup.Api/Controllers/AuthController.cs`,在 `login` 与 `refresh` action 上加特性。文件顶部 using 加 `using System.Threading.RateLimiting;`(注意 `[EnableRateLimiting]` 在 `Microsoft.AspNetCore.RateLimiting` 命名空间):

```csharp
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(typeof(TokenResponse), Status200OK)]
    [ProducesResponseType(typeof(object), Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    { ... }
```
对 `Refresh` action 同样加 `[EnableRateLimiting("auth-login")]`。
顶部 using 加 `using Microsoft.AspNetCore.RateLimiting;`。

- [ ] **Step 4: 构建验证**

Run: `dotnet build backend/src/OneCup.Api/OneCup.Api.csproj`
Expected: 构建成功。

- [ ] **Step 5: 提交**

```bash
git add backend/src/OneCup.Api/Program.cs backend/src/OneCup.Api/Controllers/AuthController.cs
git commit -m "feat(sec): 登录/刷新端点限流 (固定窗口 10次/分钟/IP)"
```

---

## Task 7: 全局异常处理 — 生产环境不回显 + 安全日志

**Files:**
- Modify: `backend/src/OneCup.Api/Program.cs:104-125`(异常处理器)

**Interfaces:**
- Consumes: `AccountLockedException`(Task 4)需映射为 401。
- Produces:异常处理器在非 Development 环境返回通用 message;所有 500 异常记 `ILogger` 完整堆栈;锁定异常映射 401 且响应带 `retryAfter`。

- [ ] **Step 1: 改造异常处理器**

替换 `Program.cs:104-125` 的 `app.UseExceptionHandler(...)` 块为:

```csharp
// 全局异常处理:认证异常→401, 领域异常→400, 其他→500;生产环境不回显内部细节
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices.GetService<ILogger<Program>>();
        context.Response.ContentType = "application/json";

        // 标准错误结构
        var (statusCode, code, message, retryAfter) = exception switch
        {
            AccountLockedException ex => (StatusCodes.Status401Unauthorized, "ACCOUNT_LOCKED", (string?)ex.Message, ex.RetryAfter),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", exception?.Message, (TimeSpan?)null),
            DomainException => (StatusCodes.Status400BadRequest, "DOMAIN_ERROR", exception?.Message, (TimeSpan?)null),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", (string?)null, (TimeSpan?)null),
        };

        // 500 始终记完整堆栈;其他警告级
        if (statusCode >= 500)
        {
            logger?.LogError(exception, "未处理异常:{Type}", exception?.GetType().Name);
        }
        else
        {
            logger?.LogWarning("业务异常:{Code} {Message}", code, exception?.Message);
        }

        // 生产环境 500 不回显 message
        var exposedMessage = statusCode >= 500 && !app.Environment.IsDevelopment()
            ? "服务器内部错误"
            : message ?? "服务器内部错误";

        context.Response.StatusCode = statusCode;
        var response = retryAfter is null
            ? (object)new { code, message = exposedMessage }
            : new { code, message = exposedMessage, retryAfter = (int)Math.Ceiling(retryAfter.Value.TotalSeconds) };
        await context.Response.WriteAsJsonAsync(response);
    });
});
```

- [ ] **Step 2: 处理构造函数参数与 LoginRequest**

确认 `LoginRequest` 构造函数支持 `new LoginRequest("admin", "pass")`。读 `backend/src/OneCup.Application/Dtos/Auth/AuthDtos.cs` 确认;若是 record 且位置参数顺序为 `(string Username, string Password)`,直接可用;否则改测试用对象初始化器 `new LoginRequest { Username = "admin", Password = "pass" }`。**在 Task 5 的测试写完后立即核对一次,据此修正。**

- [ ] **Step 3: using 补全与构建**

`Program.cs` 顶部 using 确保含 `using OneCup.Domain.Exceptions;`(已有)。`ILogger<Program>` 可用,因 `Program` 是顶层语句生成的 partial 类。

Run: `dotnet build backend/src/OneCup.Api/OneCup.Api.csproj`
Expected: 构建成功。

- [ ] **Step 4: 运行全部测试**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: PASS(异常处理器变更不影响单元测试,但确认无回归)。

- [ ] **Step 5: 提交**

```bash
git add backend/src/OneCup.Api/Program.cs
git commit -m "feat(sec): 全局异常处理 生产环境不回显内部细节 + 安全日志"
```

---

## Task 8: token 吊销与权限拒绝日志

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/Services/AuthService.cs`(Refresh/Logout 记日志)
- Create: `backend/src/OneCup.Api/Authorization/AuthorizationAuditLogger.cs`(权限拒绝日志)
- Modify: `backend/src/OneCup.Api/Authorization/WildcardAuthorizationHandler.cs` 或新增 handler(记拒绝)
- Test: `backend/tests/OneCup.UnitTests/Auth/AuthServiceTests.cs`(日志行为通过可观测副作用验证)

**Interfaces:**
- Consumes: Task 5 已注入的 `ILogger<AuthService>`。
- Produces:Refresh 轮换吊销记 Information;Logout 吊销记 Information;权限拒绝经新 handler 记 Warning。

> 日志断言策略:不直接断言 ILogger 调用(避免引入 mock 框架破坏手写 fake 风格)。通过 `TestLogger`(捕获日志条目的简单 fake)在 AuthServiceTests 验证关键路径触发日志。鉴于复杂度,本轮日志测试采用"行为已验证 + 日志通过冒烟人工确认"的策略,日志单测留作后续;但 AuthService 测试中已有的 fake logger(NullLogger)保持兼容。

- [ ] **Step 1: 在 RefreshAsync 加日志**

修改 `AuthService.cs` 的 `RefreshAsync`,在吊销旧 token 处(第 57-59 行)加:

```csharp
        // 轮换:吊销旧 token
        stored.IsRevoked = true;
        stored.UpdatedAt = DateTime.UtcNow;
        _logger.LogInformation("Refresh token 轮换吊销:UserId={UserId}, Token={TokenMask}",
            stored.User.Id, MaskToken(stored.Token));
```
在 `LogoutAsync` 的 foreach 后(`SaveChangesAsync` 前)加:

```csharp
        _logger.LogInformation("登出吊销 refresh token:UserId={UserId}, 数量={Count}", userId, tokens.Count);
```

并在 AuthService 末尾加私有方法:

```csharp
    /// <summary>掩码 token,只保留前 8 字符用于日志识别。</summary>
    private static string MaskToken(string token) =>
        token.Length <= 8 ? "****" : $"{token[..8]}****";
```

- [ ] **Step 2: 新增权限拒绝日志 handler**

```csharp
// backend/src/OneCup.Api/Authorization/AuthorizationAuditHandler.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace OneCup.Api.Authorization;

/// <summary>
/// 在授权失败(403)时记录安全审计日志。
/// 通过 IAuthorizationMiddlewareResultHandler 装饰,不改变授权结果。
/// </summary>
public class AuthorizationAuditHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _inner = new();
    private readonly ILogger<AuthorizationAuditHandler> _logger;

    public AuthorizationAuditHandler(ILogger<AuthorizationAuditHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        IAuthorizationResult result)
    {
        if (!result.Succeeded)
        {
            var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var endpoint = context.GetEndpoint()?.DisplayName;
            _logger.LogWarning("权限拒绝:UserId={UserId}, Endpoint={Endpoint}", userId, endpoint);
        }
        await _inner.HandleAsync(next, context, policy, result);
    }
}
```

- [ ] **Step 3: 注册 AuthorizationAuditHandler**

在 `Program.cs` Service 注册块加:

```csharp
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, AuthorizationAuditHandler>();
```
顶部 using 加 `using OneCup.Api.Authorization;`(已有)。

- [ ] **Step 4: 构建并运行测试**

Run: `dotnet build backend/src/OneCup.Api && dotnet test backend/tests/OneCup.UnitTests`
Expected: 构建成功,测试 PASS。

- [ ] **Step 5: 提交**

```bash
git add backend/src/OneCup.Infrastructure/Services/AuthService.cs backend/src/OneCup.Api/Authorization/AuthorizationAuditHandler.cs backend/src/OneCup.Api/Program.cs
git commit -m "feat(sec): token 吊销日志 + 权限拒绝审计日志"
```

---

## Task 9: 阶段 A 冒烟验证

**Files:** 无新增/修改(验证型)

- [ ] **Step 1: 全量测试**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: 全部 PASS。

- [ ] **Step 2: 启动校验 fail-fast 验证**

不覆盖 SecretKey 直接启动,应启动失败:

Run: `cd backend/src/OneCup.Api && dotnet run` (占位符密钥)
Expected: 启动抛 `OptionsValidationException`(SecretKey 占位符),进程退出。
(Ctrl+C 终止)

用 user-secrets 设置合规密钥后重新启动:

Run: `cd backend/src/OneCup.Api && dotnet user-secrets set "Jwt:SecretKey" "a-valid-dev-secret-key-at-least-32-bytes!!" && dotnet run`
Expected: 正常启动。

- [ ] **Step 3: 限流验证**

对 `/api/auth/login` 连发 11 次请求(同一 IP):

Run(示意):
```bash
for i in $(seq 1 11); do curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:PORT/api/auth/login -H "Content-Type: application/json" -d '{"username":"x","password":"y"}'; done
```
Expected: 前 10 次返回 401(凭证错),第 11 次返回 **429**。

- [ ] **Step 4: 锁定验证**

对 admin 账号连发 5 次错误密码:

Run(示意,需连续):5 次 `POST /api/auth/login` 错误密码
Expected: 第 5 次后返回 `{"code":"ACCOUNT_LOCKED",...,"retryAfter":900}`。

- [ ] **Step 5: 提交收尾(如有遗漏的 appsettings 变更)**

```bash
git add -A
git commit -m "chore(sec): 阶段A 冒烟验证通过" --allow-empty
```

---

## Self-Review(写作后自检,已完成)

**1. Spec 覆盖(spec 第 3 节 A 安全基线):**
- 3.1 SecretKey 校验 → Task 1 ✓
- 3.2 限流+锁定 → Task 4(锁定)+ Task 6(限流)+ Task 5(集成到 AuthService)✓
- 3.3 异常不回显 → Task 7 ✓
- 3.4 安全日志 → Task 5(登录)+ Task 8(token 吊销/权限拒绝)✓
- 3.5 admin 通配收敛 → Task 2 + Task 3 ✓

**2. 占位符扫描:** 无 TBD/TODO。LoginRequest 构造在 Task 7 Step 2 标注了"需核对"。已补具体核对动作。✓

**3. 类型一致性:**
- `IPermissionCalculator` 在 Task 2 定义,Task 3 三处接入签名一致 ✓
- `ILockoutStore` 在 Task 4 定义,Task 5 接入 + fake 实现一致 ✓
- `AccountLockedException` Task 4 定义,Task 5 抛出、Task 7 映射一致 ✓
- `AuthService` 构造函数逐步演进:Task 3 加 permCalc,Task 5 加 lockout+logger,测试 `CreateAuthService` 同步 ✓
