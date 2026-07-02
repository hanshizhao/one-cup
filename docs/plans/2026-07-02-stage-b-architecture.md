# 阶段 B:后端架构修正(标准 Clean Architecture)实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 4 个业务 Service(Auth/User/Role/Permission)从 Infrastructure 迁回 Application 层,引入 Specification 规范模式,真正启用扩展后的 IRepository + IUnitOfWork,使 Application 层零 EF Core 依赖。

**Architecture:** 业务 Service 放 `OneCup.Application/Services/`,依赖 `IRepository<T>`/`IUnitOfWork`/`ISpecification<T>` 抽象,不引用 EF Core。EF Core 配置、DbContext、Repository 实现、JwtTokenService、PasswordHasher 留在 Infrastructure(技术细节)。Specification 在 Application 层定义,Service 构造规范、Repository 翻译为 LINQ。测试保持集成测试风格(测试项目引用 Application + Infrastructure,用 EF InMemory + 真实 Repository 包装)。

**Tech Stack:** .NET 10 / EF Core / Specification 模式 / xUnit + EF Core InMemory。

## Global Constraints

- net10.0;Nullable + ImplicitUsings on。
- **Application 层零 EF Core NuGet 引用**(验证标准:`dotnet list backend/src/OneCup.Application package` 无任何 EFCore/EntityFramework 项)。Application 只引用 Domain + `Microsoft.Extensions.Options`(现状)。
- JwtTokenService、PasswordHasher **留在 Infrastructure**(它们依赖 IdentityModel/BCrypt 加密库,是技术细节)。
- 测试保持**集成测试风格**:测试项目引用 Application + Infrastructure,用 EF InMemory DbContext 构造真实 Repository,再注入 Service。
- 测试风格:xUnit `[Fact]`、手写 fake(无 Moq)。
- `IAuthService`/`IUserService`/`IRoleService`/`IPermissionService` 接口签名**不变**。
- 现有 `IRepository<T>` 与 `IUnitOfWork` 定义在同一文件 `backend/src/OneCup.Application/Interfaces/IUnitOfWork.cs`。
- 业务规则保持不变:admin 保护、角色删除关联用户校验、登录失败锁定、通配权限——这些是阶段A已验证的行为,迁移不得改变语义。
- `SeedData.AdminUserId` 等是 internal(在 Infrastructure),被 UserService 业务保护引用。迁移 Service 到 Application 前,需把 admin 相关常量提升为 Application 层 public 常量。
- `OneCup.UnitTests` 当前只引用 Domain + Infrastructure,需新增引用 Application。
- **Numbering 模块(同事合并)处理**:NumberingClock、NumberingRuleService 纳入迁移(同 User/Role 范式);**NumberingService 暂留 Infrastructure**——它用 `FromSqlRaw FOR UPDATE` 行锁 + 调用方持事务守卫 + ChangeTracker 手动 Detach + PG 23505 异常解析,Repository/Specification 抽象力不从心,属持久化/并发基础设施职责。
- `IUnitOfWork` 须新增**事务抽象**(`ExecuteInTransactionAsync` / `IApplicationTransaction`),供 Application 层 Service 表达事务边界,并支持 NumberingService 这类要求调用方持事务的场景。
- `IApplicationTransaction` 是 Application 层抽象,不泄漏 EF Core 的 `IDbContextTransaction` 类型;Infrastructure 实现包装它。

---

## File Structure

| 文件 | 责任 | 动作 |
|------|------|------|
| `OneCup.Application/Specifications/ISpecification.cs` | 规范接口 | Create |
| `OneCup.Application/Specifications/Specification.cs` | 规范基类(链式构造) | Create |
| `OneCup.Application/Interfaces/IUnitOfWork.cs` | 扩展 IRepository(加 Specification 重载) | Modify |
| `OneCup.Infrastructure/Persistence/Repository.cs` | 实现 Specification 翻译 | Modify |
| `OneCup.Application/Common/SystemConstants.cs` | admin userId/roleId 等 public 常量 | Create |
| `OneCup.Application/Services/UserService.cs` | 迁移自 Infrastructure | Move+Modify |
| `OneCup.Application/Services/RoleService.cs` | 迁移 | Move+Modify |
| `OneCup.Application/Services/PermissionService.cs` | 迁移 | Move+Modify |
| `OneCup.Application/Services/AuthService.cs` | 迁移 | Move+Modify |
| `OneCup.Application/Services/NumberingClock.cs` | 迁移(纯时钟,零依赖) | Move |
| `OneCup.Application/Services/NumberingRuleService.cs` | 迁移(CRUD+分页,Repository 范式) | Move+Modify |
| `OneCup.Application/Interfaces/IApplicationTransaction.cs` | 事务抽象(不泄漏 EF 类型) | Create |
| `OneCup.Infrastructure/Persistence/UnitOfWork.cs` | 实现事务抽象(包装 IDbContextTransaction) | Modify |
| `OneCup.Infrastructure/Services/NumberingService.cs` | 留 Infrastructure,适配事务抽象 | Modify |
| `OneCup.Application/Interfaces/ILockoutStore.cs` | 从 Infrastructure 提升到 Application(AuthService 依赖) | Move |
| `OneCup.Infrastructure/Lockout/MemoryLockoutStore.cs` | 实现留在 Infrastructure,接口引用改 Application | Modify |
| `OneCup.Api/Program.cs` | using 更新 + DI(无逻辑变) | Modify |
| 测试文件 | 适配新构造函数(注入 Repository/UoW) | Modify |

---

## Task 1: ISpecification 规范模式骨架

**Files:**
- Create: `backend/src/OneCup.Application/Specifications/ISpecification.cs`
- Create: `backend/src/OneCup.Application/Specifications/Specification.cs`
- Test: `backend/tests/OneCup.UnitTests/Specifications/SpecificationTests.cs`

**Interfaces:**
- Produces: `ISpecification<T>`(属性:`Criteria`、`Includes`、`OrderBy`、`OrderByDescending`、`Skip?`、`Take?`)、抽象基类 `Specification<T>`(链式 Apply 方法)。

> **Includes 用字符串路径设计**(如 `"Roles"`、`"Roles.Permissions"`):EF Core 的 `Include(string)` 重载原生支持点分多级路径,直接覆盖单层 Include 与多级 ThenInclude 场景(Role.Permissions、User.Roles.Permissions)。这比表达式树 `Expression<Func<T,object>>` + ThenInclude 简单得多,且 Application 层不引入任何 EF Core 类型。
>
> `Criteria` 用 `Expression<Func<T,bool>>`(System.Linq.Expressions,.NET 基础库,非 EF Core),`OrderBy` 同理。

- [ ] **Step 1: 写失败测试**

```csharp
// backend/tests/OneCup.UnitTests/Specifications/SpecificationTests.cs
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;

namespace OneCup.UnitTests.Specifications;

public class SpecificationTests
{
    [Fact]
    public void Empty_specification_has_no_criteria_or_paging()
    {
        ISpecification<User> spec = new TestSpec();
        Assert.Null(spec.Criteria);
        Assert.Empty(spec.Includes);
        Assert.Null(spec.Skip);
        Assert.Null(spec.Take);
    }

    [Fact]
    public void Apply_criteria_sets_criteria()
    {
        var spec = new TestSpec();
        spec.ApplyCriteria(u => u.Username == "admin");
        Assert.NotNull(spec.Criteria);
    }

    [Fact]
    public void ApplyInclude_adds_string_path()
    {
        var spec = new TestSpec();
        spec.ApplyInclude("Roles");
        spec.ApplyInclude("Roles.Permissions");
        Assert.Equal(2, spec.Includes.Count);
        Assert.Contains("Roles.Permissions", spec.Includes);
    }

    [Fact]
    public void ApplyPaging_sets_skip_take()
    {
        var spec = new TestSpec();
        spec.ApplyPaging(page: 2, pageSize: 10);
        Assert.Equal(10, spec.Skip);
        Assert.Equal(10, spec.Take);
    }

    // 测试用具体类(基类是抽象的)
    internal class TestSpec : Specification<User> { }
}
```

- [ ] **Step 2: 运行验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~SpecificationTests"`
Expected: FAIL — 命名空间/类型不存在。

- [ ] **Step 3: 定义 ISpecification**

```csharp
// backend/src/OneCup.Application/Specifications/ISpecification.cs
using System.Linq.Expressions;

namespace OneCup.Application.Specifications;

/// <summary>
/// 查询规范:把查询条件、关联加载、排序、分页封装为一个对象,
/// 由 Infrastructure 的 Repository 翻译为 EF Core LINQ。
/// Application 层通过它表达查询意图,不直接依赖 EF Core。
/// </summary>
public interface ISpecification<T>
{
    /// <summary>过滤条件(可空表示无条件)。</summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>需要 Include 的导航属性(字符串路径,支持点分多级如 "Roles.Permissions")。</summary>
    IReadOnlyList<string> Includes { get; }

    /// <summary>升序排序键(可空)。</summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>降序排序键(可空)。</summary>
    Expression<Func<T, object>>? OrderByDescending { get; }

    /// <summary>跳过条数(分页用,可空)。</summary>
    int? Skip { get; }

    /// <summary>取条数(分页用,可空)。</summary>
    int? Take { get; }
}
```

- [ ] **Step 4: 定义 Specification 基类**

```csharp
// backend/src/OneCup.Application/Specifications/Specification.cs
using System.Linq.Expressions;

namespace OneCup.Application.Specifications;

/// <summary>
/// 规范基类:子类在构造函数中用 Apply* 方法链式构造查询。
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    public Expression<Func<T, bool>>? Criteria { get; private set; }
    public List<string> Includes { get; } = new();
    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }
    public int? Skip { get; private set; }
    public int? Take { get; private set; }

    IReadOnlyList<string> ISpecification<T>.Includes => Includes;

    protected void ApplyCriteria(Expression<Func<T, bool>> criteria) => Criteria = criteria;
    protected void ApplyInclude(string navigationPath) => Includes.Add(navigationPath);
    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;
    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescending) =>
        OrderByDescending = orderByDescending;
    protected void ApplyPaging(int page, int pageSize)
    {
        Skip = (page - 1) * pageSize;
        Take = pageSize;
    }
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~SpecificationTests"`
Expected: PASS(4 用例)。

- [ ] **Step 6: 提交**

```bash
git add backend/src/OneCup.Application/Specifications/ backend/tests/OneCup.UnitTests/Specifications/
git commit -m "feat(arch): ISpecification 规范模式骨架"
```

---

## Task 2: 扩展 IRepository + Repository 实现 Specification 翻译

**Files:**
- Modify: `backend/src/OneCup.Application/Interfaces/IUnitOfWork.cs`(IRepository 加重载)
- Modify: `backend/src/OneCup.Infrastructure/Persistence/Repository.cs`(实现翻译)
- Test: `backend/tests/OneCup.UnitTests/Persistence/RepositorySpecificationTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `ISpecification<T>`。
- Produces: 扩展后的 `IRepository<T>`:
  - `Task<T?> GetByIdAsync(Guid id, ct)`
  - `Task<IReadOnlyList<T>> ListAsync(ISpecification<T>? spec, ct)`(重载,原无参 ListAsync 保留为 `spec:null` 调用)
  - `Task<int> CountAsync(ISpecification<T>? spec, ct)`
  - `Task<bool> AnyAsync(ISpecification<T>? spec, ct)`
  - `Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, ct)`
  - `Task AddAsync(T, ct)` / `void Update(T)` / `void Remove(T)`

- [ ] **Step 1: 写失败测试**

```csharp
// backend/tests/OneCup.UnitTests/Persistence/RepositorySpecificationTests.cs
using Microsoft.EntityFrameworkCore;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Persistence;

public class RepositorySpecificationTests
{
    private static async Task<(Repository<User> repo, OneCupDbContext db)> SetupAsync(string dbName)
    {
        var options = new DbContextOptionsBuilder<OneCupDbContext>().UseInMemoryDatabase(dbName).Options;
        var db = new OneCupDbContext(options);
        db.Users.AddRange(
            new User { Username = "admin", DisplayName = "管理员", Roles = new List<Role>() },
            new User { Username = "alice", DisplayName = "Alice", Roles = new List<Role>() });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (new Repository<User>(db), db);
    }

    private class UserByUsernameSpec : Specification<User>
    {
        public UserByUsernameSpec(string username) { ApplyCriteria(u => u.Username == username); ApplyInclude("Roles"); }
    }
    private class UserPagedSpec : Specification<User>
    {
        public UserPagedSpec(string? keyword, int page, int size)
        {
            if (!string.IsNullOrWhiteSpace(keyword)) ApplyCriteria(u => u.Username.Contains(keyword));
            ApplyOrderByDescending(u => u.CreatedAt);
            ApplyPaging(page, size);
        }
    }

    [Fact]
    public async Task FirstOrDefaultAsync_with_spec_finds_and_includes()
    {
        var (repo, _) = await SetupAsync(nameof(FirstOrDefaultAsync_with_spec_finds_and_includes));
        var user = await repo.FirstOrDefaultAsync(new UserByUsernameSpec("admin"), default);
        Assert.NotNull(user);
        Assert.Equal("admin", user!.Username);
    }

    [Fact]
    public async Task CountAsync_with_spec_counts_matching()
    {
        var (repo, _) = await SetupAsync(nameof(CountAsync_with_spec_counts_matching));
        var spec = new UserByUsernameSpec("admin");
        Assert.Equal(1, await repo.CountAsync(spec, default));
        Assert.Equal(2, await repo.CountAsync(null, default));
    }

    [Fact]
    public async Task ListAsync_with_paging_respects_skip_take()
    {
        var (repo, _) = await SetupAsync(nameof(ListAsync_with_paging_respects_skip_take));
        var page1 = await repo.ListAsync(new UserPagedSpec(null, 1, 1), default);
        var page2 = await repo.ListAsync(new UserPagedSpec(null, 2, 1), default);
        Assert.Single(page1);
        Assert.Single(page2);
        Assert.NotEqual(page1[0].Username, page2[0].Username);
    }

    [Fact]
    public async Task AnyAsync_with_spec()
    {
        var (repo, _) = await SetupAsync(nameof(AnyAsync_with_spec));
        Assert.True(await repo.AnyAsync(new UserByUsernameSpec("admin"), default));
        Assert.False(await repo.AnyAsync(new UserByUsernameSpec("nobody"), default));
    }
}
```

> 注:`Repository<T>` 构造当前是 `Repository<T>(OneCupDbContext)`。EF InMemory 的 `Include` 对导航属性在 InMemory provider 下行为有限(InMemory 不完全支持关系查询),但 `FirstOrDefaultAsync` 的 `Where` 过滤会生效;Include 在 InMemory 下基本是 no-op,这对此测试可接受(我们测的是规范翻译与分页/计数,非关联数据加载——关联加载的正确性靠 Application 的 Service 迁移后的集成测试保证)。若 Include 断言需要,改用 `.Roles` 非空检查需谨慎,先验证 InMemory 行为。

- [ ] **Step 2: 运行验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~RepositorySpecificationTests"`
Expected: FAIL — IRepository 无新方法。

- [ ] **Step 3: 扩展 IRepository 接口**

修改 `backend/src/OneCup.Application/Interfaces/IUnitOfWork.cs` 中的 `IRepository<T>`,在现有方法后加:

```csharp
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);
```
保留原无参 `ListAsync()`(等价于 `ListAsync(null)`)。文件顶部加 `using OneCup.Application.Specifications;`。

- [ ] **Step 4: 实现 Repository 的 Specification 翻译**

修改 `backend/src/OneCup.Infrastructure/Persistence/Repository.cs`,加私有方法 `ApplySpecification` 与新公开方法:

```csharp
    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(spec);
        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default)
        => await ApplySpecification(spec).CountAsync(cancellationToken);

    public async Task<bool> AnyAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default)
        => await ApplySpecification(spec).AnyAsync(cancellationToken);

    public async Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
        => await ApplySpecification(spec).FirstOrDefaultAsync(cancellationToken);

    private IQueryable<T> ApplySpecification(ISpecification<T>? spec)
    {
        var query = _db.Set<T>().AsQueryable();
        if (spec is null) return query;
        if (spec.Criteria is not null) query = query.Where(spec.Criteria);
        foreach (var include in spec.Includes) query = query.Include(include);   // 字符串路径,EF Core 支持 "Roles.Permissions"
        if (spec.OrderBy is not null) query = query.OrderBy(spec.OrderBy);
        if (spec.OrderByDescending is not null) query = query.OrderByDescending(spec.OrderByDescending);
        if (spec.Skip is not null) query = query.Skip(spec.Skip.Value);
        if (spec.Take is not null) query = query.Take(spec.Take.Value);
        return query;
    }
```
顶部 using 确保有 `using Microsoft.EntityFrameworkCore;`、`using OneCup.Application.Specifications;`。原无参 `ListAsync()` 改为委托:`public Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default) => ListAsync(null, ct);`(签名兼容旧调用)。

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~RepositorySpecificationTests"`
Expected: PASS(4 用例)。

- [ ] **Step 6: 提交**

```bash
git add backend/src/OneCup.Application/Interfaces/IUnitOfWork.cs backend/src/OneCup.Infrastructure/Persistence/Repository.cs backend/tests/OneCup.UnitTests/Persistence/
git commit -m "feat(arch): 扩展 IRepository 支持 Specification 查询"
```

---

## Task 3: SystemConstants(admin 常量提升到 Application)

**Files:**
- Create: `backend/src/OneCup.Application/Common/SystemConstants.cs`
- Modify: `backend/src/OneCup.Infrastructure/Services/UserService.cs`(业务保护引用改为 SystemConstants)— 注意:本 Task 只改引用指向,文件还在 Infrastructure(下个 Task 才迁移)。或:若 UserService 在本 Task 尚未迁移,先让 Infrastructure 的 UserService 引用 Application 的 SystemConstants(Infrastructure 已引用 Application,可行)。

**Interfaces:**
- Produces: `SystemConstants.AdminUserId`(Guid)、`SystemConstants.AdminRoleCode = "admin"`、`SystemConstants.AdminRoleId`(Guid)等 public 常量。

> 这一步是为了解除迁移后 Application 层 Service 对 Infrastructure 的 `SeedData`(internal)的依赖。

- [ ] **Step 1: 创建 SystemConstants**

```csharp
// backend/src/OneCup.Application/Common/SystemConstants.cs
namespace OneCup.Application.Common;

/// <summary>
/// 系统级公开常量。供 Application 层 Service 做业务保护(admin 账号/角色),
/// 不依赖 Infrastructure 的 internal SeedData。
/// </summary>
public static class SystemConstants
{
    /// <summary>内置 admin 用户 Id(与 SeedData 一致)。</summary>
    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>内置 admin 角色 Id。</summary>
    public static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    /// <summary>admin 角色编码。</summary>
    public const string AdminRoleCode = "admin";
}
```

- [ ] **Step 2: 让 Infrastructure 的 UserService/RoleService 引用它**

读 `backend/src/OneCup.Infrastructure/Services/UserService.cs` 与 `RoleService.cs`,把对 `SeedData.AdminUserId`、`SeedData.AdminRoleId`、`"admin"` 字面量的业务保护引用改为 `SystemConstants.*`。加 `using OneCup.Application.Common;`。保留 Infrastructure 的 `SeedData` 原样(它仍用于种子数据),只是业务保护逻辑不再读它。

- [ ] **Step 3: 构建验证**

Run: `dotnet build backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj`
Expected: 成功。

- [ ] **Step 4: 运行测试(无回归)**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: 全 PASS。

- [ ] **Step 5: 提交**

```bash
git add backend/src/OneCup.Application/Common/SystemConstants.cs backend/src/OneCup.Infrastructure/Services/UserService.cs backend/src/OneCup.Infrastructure/Services/RoleService.cs
git commit -m "feat(arch): SystemConstants 提升到 Application (解除对 SeedData 的业务耦合)"
```

---

## Task 4: ILockoutStore 接口提升到 Application

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/ILockoutStore.cs`
- Delete: `backend/src/OneCup.Infrastructure/Interfaces/ILockoutStore.cs`
- Modify: `backend/src/OneCup.Infrastructure/Lockout/MemoryLockoutStore.cs`(实现引用改 Application 接口)
- Modify: `backend/tests/OneCup.UnitTests/Lockout/MemoryLockoutStoreTests.cs`(using 改 Application)
- Modify: `backend/tests/OneCup.UnitTests/Auth/AuthServiceTests.cs`(FakeLockoutStore 实现的接口 using 改 Application)

> 原因:AuthService 要迁到 Application(Task 6),但它依赖 `ILockoutStore`(当前在 Infrastructure.Interfaces),会形成 Application→Infrastructure 循环依赖。把接口提到 Application。

- [ ] **Step 1: 创建 Application 层 ILockoutStore**

把 `backend/src/OneCup.Infrastructure/Interfaces/ILockoutStore.cs` 的内容移到 `backend/src/OneCup.Application/Interfaces/ILockoutStore.cs`,命名空间改为 `OneCup.Application.Interfaces`。删除原 Infrastructure 文件。

- [ ] **Step 2: 修改 MemoryLockoutStore 实现引用**

`backend/src/OneCup.Infrastructure/Lockout/MemoryLockoutStore.cs`:using 从 `OneCup.Infrastructure.Interfaces` 改为 `OneCup.Application.Interfaces`,实现接口不变。

- [ ] **Step 3: 修改测试 using**

`MemoryLockoutStoreTests.cs`、`AuthServiceTests.cs`(FakeLockoutStore)的 using `OneCup.Infrastructure.Interfaces` 改为 `OneCup.Application.Interfaces`。

- [ ] **Step 4: 构建并测试**

Run: `dotnet build backend/src/OneCup.Api/OneCup.Api.csproj && dotnet test backend/tests/OneCup.UnitTests`
Expected: 构建成功,全 PASS。

- [ ] **Step 5: 提交**

```bash
git add -A backend/src/OneCup.Application/Interfaces/ILockoutStore.cs backend/src/OneCup.Infrastructure/ backend/tests/OneCup.UnitTests/
git commit -m "refactor(arch): ILockoutStore 接口提升到 Application (解除循环依赖)"
```

---

## Task 5: 迁移 PermissionService(最简单,先练手)

**Files:**
- Move: `backend/src/OneCup.Infrastructure/Services/PermissionService.cs` → `backend/src/OneCup.Application/Services/PermissionService.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`(using 更新)
- Test: `backend/tests/OneCup.UnitTests/System/PermissionServiceTests.cs`(若无则新建,验证迁移后行为)

**Interfaces:**
- Consumes: Task 2 的 `IRepository<Permission>`。
- Produces: `PermissionService` 在 Application,依赖 `IRepository<Permission>` + 一个 `AllPermissionsSpec`。

> 选 PermissionService 先迁移:查询最简单(只读 OrderBy 投影),无写操作、无复杂业务规则,用来验证迁移流程 + Specification 用法。

- [ ] **Step 1: 定义 AllPermissionsSpec**

```csharp
// backend/src/OneCup.Application/Specifications/PermissionSpecs.cs
using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>查询全部权限(按 Code 排序)。</summary>
public class AllPermissionsSpec : Specification<Permission>
{
    public AllPermissionsSpec() { ApplyOrderBy(p => p.Code); }
}
```

- [ ] **Step 2: 迁移 PermissionService**

新建 `backend/src/OneCup.Application/Services/PermissionService.cs`,内容:

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;

namespace OneCup.Application.Services;

public class PermissionService : IPermissionService
{
    private readonly IRepository<Permission> _permissions;

    public PermissionService(IRepository<Permission> permissions)
    {
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<PermissionDto>> GetListAsync(CancellationToken ct = default)
    {
        var list = await _permissions.ListAsync(new AllPermissionsSpec(), ct);
        return list.Select(p => new PermissionDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Description = p.Description,
        }).ToList();
    }
}
```
删除原 `backend/src/OneCup.Infrastructure/Services/PermissionService.cs`。

- [ ] **Step 3: 更新 Program.cs DI**

`Program.cs` 中 `AddScoped<IPermissionService, PermissionService>()` 保留;using 把 `OneCup.Infrastructure.Services` 里关于 PermissionService 的移除,加 `using OneCup.Application.Services;`(若已有则跳过)。DI 容器会自动解析新命名空间的实现。

- [ ] **Step 4: 写/更新测试**

```csharp
// backend/tests/OneCup.UnitTests/System/PermissionServiceTests.cs
using Microsoft.EntityFrameworkCore;
using OneCup.Application.Services;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.System;

public class PermissionServiceTests
{
    private static async Task<PermissionService> SetupAsync(string dbName)
    {
        var options = new DbContextOptionsBuilder<OneCupDbContext>().UseInMemoryDatabase(dbName).Options;
        var db = new OneCupDbContext(options);
        db.Permissions.AddRange(
            new Permission { Code = "system:role:manage", Name = "管理角色" },
            new Permission { Code = "fabric:read", Name = "查看面料" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new PermissionService(new Repository<Permission>(db));
    }

    [Fact]
    public async Task GetList_returns_permissions_ordered_by_code()
    {
        var svc = await SetupAsync(nameof(GetList_returns_permissions_ordered_by_code));
        var result = await svc.GetListAsync();
        Assert.Equal(2, result.Count);
        Assert.Equal("fabric:read", result[0].Code);          // f < s
        Assert.Equal("system:role:manage", result[1].Code);
    }
}
```
确保测试项目 csproj 引用 Application(Task 0 或本 Task 前置:见下方"前置准备")。

- [ ] **Step 5: 前置——测试项目引用 Application**

修改 `backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj`,在 ProjectReference 区加:
```xml
    <ProjectReference Include="..\..\src\OneCup.Application\OneCup.Application.csproj" />
```

- [ ] **Step 6: 构建并测试**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: 全 PASS(含新 PermissionServiceTests)。

- [ ] **Step 7: 提交**

```bash
git add backend/src/OneCup.Application/Services/PermissionService.cs backend/src/OneCup.Application/Specifications/PermissionSpecs.cs backend/src/OneCup.Infrastructure/Services/PermissionService.cs(删除) backend/src/OneCup.Api/Program.cs backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj backend/tests/OneCup.UnitTests/System/PermissionServiceTests.cs
git commit -m "feat(arch): PermissionService 迁移到 Application (Specification 首例)"
```

---

## Task 6: 迁移 UserService(最复杂:分页+过滤+Include+批量加载)

**Files:**
- Move: `backend/src/OneCup.Infrastructure/Services/UserService.cs` → `backend/src/OneCup.Application/Services/UserService.cs`
- Create: `backend/src/OneCup.Application/Specifications/UserSpecs.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`
- Modify: `backend/tests/OneCup.UnitTests/System/UserServiceTests.cs`(构造方式改为注入 Repository)

**Interfaces:**
- Consumes: Task 2 的 `IRepository<User>`/`IRepository<Role>`、Task 3 的 `SystemConstants`、`IPasswordHasher`(接口在 Application)、`IUnitOfWork`。
- Produces: `UserService` 在 Application,所有 EF 查询改用 Specification + Repository。

- [ ] **Step 1: 定义 UserSpecs**

```csharp
// backend/src/OneCup.Application/Specifications/UserSpecs.cs
using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>用户分页查询(含 keyword 过滤、按创建时间倒序、Include Roles)。</summary>
public class UserPagedSpec : Specification<User>
{
    public UserPagedSpec(string? keyword, int page, int pageSize)
    {
        if (!string.IsNullOrWhiteSpace(keyword))
            ApplyCriteria(u => u.Username.Contains(keyword) || u.DisplayName.Contains(keyword));
        ApplyInclude("Roles");
        ApplyOrderByDescending(u => u.CreatedAt);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>按 Id 查询用户(含 Roles)。</summary>
public class UserByIdWithRolesSpec : Specification<User>
{
    public UserByIdWithRolesSpec(Guid id)
    {
        ApplyCriteria(u => u.Id == id);
        ApplyInclude("Roles");
    }
}

/// <summary>按用户名查询用户(含 Roles+Permissions,登录/当前用户用)。</summary>
public class UserByUsernameWithRolesSpec : Specification<User>
{
    public UserByUsernameWithRolesSpec(string username)
    {
        ApplyCriteria(u => u.Username == username);
        ApplyInclude("Roles");
        ApplyInclude("Roles.Permissions");
    }
}
```

> `UserByUsernameWithRolesSpec` 同时 Include `"Roles"` 和 `"Roles.Permissions"`(多级,字符串路径原生支持),供 AuthService 登录/当前用户场景使用。UserService 自身只用前两个 spec(分页、按 Id,仅需 Roles)。该 spec 定义在 UserSpecs 是因为它查询的是 User 实体,Auth 与 User 都可能复用。

- [ ] **Step 2: 迁移 UserService(改用 Repository)**

新建 `backend/src/OneCup.Application/Services/UserService.cs`。把原 Infrastructure UserService 的所有业务逻辑搬过来,但:
- `OneCupDbContext _db` → 改为 `IRepository<User> _users` + `IRepository<Role> _roles` + `IUnitOfWork _uow`
- 查询全部改用 Specification:
  - GetListAsync:`CountAsync(new UserPagedSpec(keyword,1,int.MaxValue))` 算总数 + `ListAsync(new UserPagedSpec(keyword,page,pageSize))` 取页;DTO 投影保留
  - GetByIdAsync:`FirstOrDefaultAsync(new UserByIdWithRolesSpec(id))`
  - CreateAsync 唯一性:`AnyAsync(new UserBy... )` 或简单 `AnyAsync(spec by username)`
  - CreateAsync/UpdateAsync 加载 Roles:`ListAsync` 一个 by-ids spec 或循环 `GetByIdAsync`(Role 无 Include 需求,简单 GetById 即可;批量可用 `RolesByIdsSpec`)
  - 写入:`AddAsync` + `await _uow.SaveChangesAsync()`
- admin 保护逻辑引用 `SystemConstants.AdminUserId`/`AdminRoleCode`(Task 3 已就绪)
- 顶部 using:`OneCup.Application.Common`、`OneCup.Application.Specifications`、`OneCup.Application.Interfaces`、`OneCup.Domain.Entities`、`OneCup.Application.Dtos.System`
- 删除原 Infrastructure UserService

> 定义 `RolesByIdsSpec : Specification<Role>`(Criteria: `r => ids.Contains(r.Id)`)用于批量加载角色。

- [ ] **Step 3: 更新 Program.cs using**

确认 `using OneCup.Application.Services;` 存在。

- [ ] **Step 4: 改写 UserServiceTests**

读现有 `UserServiceTests.cs`。当前是 `new UserService(db, new PasswordHasher())`。改为:
```csharp
var svc = new UserService(new Repository<User>(db), new Repository<Role>(db), new UnitOfWork(db), new PasswordHasher());
```
种子数据部分(对 db.Roles/db.Users 操作)保持不变(集成测试风格)。断言部分(直接查 db)保持不变。用例逻辑保持不变。

- [ ] **Step 5: 构建并测试**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: 全 PASS。注意验证:admin 保护用例、分页用例、重名用例都通过(语义不变)。

- [ ] **Step 6: 提交**

```bash
git add backend/src/OneCup.Application/Services/UserService.cs backend/src/OneCup.Application/Specifications/UserSpecs.cs backend/src/OneCup.Infrastructure/Services/UserService.cs(删除) backend/src/OneCup.Api/Program.cs backend/tests/OneCup.UnitTests/System/UserServiceTests.cs
git commit -m "feat(arch): UserService 迁移到 Application (Specification+Repository)"
```

---

## Task 7: 迁移 RoleService + 解决 ThenInclude

**Files:**
- Move: `backend/src/OneCup.Infrastructure/Services/RoleService.cs` → `backend/src/OneCup.Application/Services/RoleService.cs`
- Create/Modify: `backend/src/OneCup.Application/Specifications/RoleSpecs.cs`
- Modify: `backend/src/OneCup.Application/Specifications/ISpecification.cs`(若决定加 ThenInclude 支持)
- Modify: `backend/src/OneCup.Infrastructure/Persistence/Repository.cs`(若加 ThenInclude 翻译)
- Modify: `backend/tests/OneCup.UnitTests/System/RoleServiceTests.cs`

**ThenInclude 处理(已在 Task 1 解决)**:
RoleService 与 AuthService 都需要加载导航属性的导航属性(Role.Permissions、User.Roles.Permissions)。Task 1 的 `Includes` 已采用**字符串路径**设计(`"Roles.Permissions"`),EF Core `Include(string)` 原生支持点分多级路径,因此无需额外机制——本 Task 直接用即可。

- [ ] **Step 1: 定义 RoleSpecs**

```csharp
// backend/src/OneCup.Application/Specifications/RoleSpecs.cs
using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

public class RoleWithPermissionsSpec : Specification<Role>
{
    public RoleWithPermissionsSpec(Guid id) { ApplyCriteria(r => r.Id == id); ApplyInclude("Permissions"); }
}

public class RoleWithPermissionsAndUsersSpec : Specification<Role>
{
    public RoleWithPermissionsAndUsersSpec(Guid id)
    {
        ApplyCriteria(r => r.Id == id);
        ApplyInclude("Permissions");
        ApplyInclude("Users");   // 用于删除前关联用户计数
    }
}

public class PermissionsByIdsSpec : Specification<Permission>
{
    public PermissionsByIdsSpec(IEnumerable<Guid> ids) { ApplyCriteria(p => ids.Contains(p.Id)); }
}
```

- [ ] **Step 2: 迁移 RoleService**

搬到 Application,改用 `IRepository<Role>` + `IRepository<Permission>` + `IUnitOfWork`。查询用上述 Spec。关联用户计数改用 `RoleWithPermissionsAndUsersSpec` 加载后 `.Users.Count`,或单独 `AnyAsync` spec。admin 角色保护用 `SystemConstants.AdminRoleCode`/`AdminRoleId`。

- [ ] **Step 3: 更新 RoleServiceTests 构造**

`new RoleService(new Repository<Role>(db), new Repository<Permission>(db), new UnitOfWork(db))`。用例逻辑不变。

- [ ] **Step 4: 构建并测试**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: 全 PASS。

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "feat(arch): RoleService 迁移到 Application (多级 Include via 字符串路径)"
```

---

## Task 8: 迁移 AuthService(RefreshToken 查询 + 多级 Include)

**Files:**
- Move: `backend/src/OneCup.Infrastructure/Services/AuthService.cs` → `backend/src/OneCup.Application/Services/AuthService.cs`
- Create: `backend/src/OneCup.Application/Specifications/AuthSpecs.cs` + UserSpecs 加多级 Include
- Modify: `backend/tests/OneCup.UnitTests/Auth/AuthServiceTests.cs`

- [ ] **Step 1: 定义 AuthSpecs + 扩展 UserSpecs**

`UserByUsernameWithRolesSpec` 改 Include 路径 `["Roles", "Roles.Permissions"]`(登录需要权限聚合)。
新增:
```csharp
public class UserByIdWithRolesPermissionsSpec : Specification<User>
{
    public UserByIdWithRolesPermissionsSpec(Guid id)
    {
        ApplyCriteria(u => u.Id == id);
        ApplyInclude("Roles");
        ApplyInclude("Roles.Permissions");
    }
}

public class RefreshTokenByTokenSpec : Specification<RefreshToken>
{
    public RefreshTokenByTokenSpec(string token)
    {
        ApplyCriteria(rt => rt.Token == token);
        ApplyInclude("User");
        ApplyInclude("User.Roles");
        ApplyInclude("User.Roles.Permissions");
    }
}

public class ActiveRefreshTokensByUserSpec : Specification<RefreshToken>
{
    public ActiveRefreshTokensByUserSpec(Guid userId) { ApplyCriteria(rt => rt.UserId == userId && !rt.IsRevoked); }
}
```

- [ ] **Step 2: 迁移 AuthService**

搬到 Application,依赖 `IRepository<User>` + `IRepository<RefreshToken>` + `IUnitOfWork` + `IJwtTokenService`(接口)+ `IPasswordHasher` + `IPermissionCalculator` + `ILockoutStore`(Task 4 已提升)+ `JwtOptions` + `ILogger<AuthService>`。所有查询改 Spec。IssueTokensAsync 的 `_db.RefreshTokens.Add` 改 `_refreshTokens.AddAsync`,`SaveChangesAsync` 走 `_uow`。

- [ ] **Step 3: 更新 AuthServiceTests 构造**

`new AuthService(new Repository<User>(db), new Repository<RefreshToken>(db), new UnitOfWork(db), jwt, passwordHasher, Options.Create(_options), permCalc, lockout, logger)`。种子数据与断言不变。

- [ ] **Step 4: 构建并测试**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: 全 PASS(含登录/刷新/登出/锁定/当前用户所有用例)。

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "feat(arch): AuthService 迁移到 Application (多级 Include + Repository)"
```

---

## Task 8a: 迁移 NumberingClock(纯时钟,零依赖)

**Files:**
- Move: `backend/src/OneCup.Infrastructure/Services/NumberingClock.cs` → `backend/src/OneCup.Application/Services/NumberingClock.cs`
- Modify: `backend/src/OneCup.Infrastructure/Services/NumberingService.cs`(它依赖 INumberingClock,接口已在 Application,实现随 NumberingClock 一起上移;NumberingService 留 Infrastructure 但构造注入的 NumberingClock 实现现在来自 Application —— DI 自动解析)
- Modify: `backend/tests/OneCup.UnitTests/`(NumberingClockTests.cs using 更新)

> NumberingClock 是纯时钟抽象(UTC→北京时间),零 DB 依赖,迁移几乎只是改命名空间。它随 NumberingService 构造注入,但实现上移到 Application 后,Infrastructure 的 NumberingService 通过 DI 拿到它(Infrastructure→Application 方向合法)。

- [ ] **Step 1: 迁移文件**

移动 `NumberingClock.cs` 到 `backend/src/OneCup.Application/Services/`,命名空间改 `OneCup.Application.Services`。内容不变(只引用 INumberingClock + BCL)。

- [ ] **Step 2: 更新测试 using**

`NumberingClockTests.cs` 的 `using OneCup.Infrastructure.Services` 改为 `using OneCup.Application.Services`。

- [ ] **Step 3: 构建并测试**

Run: `dotnet build backend/src/OneCup.Api/OneCup.Api.csproj && dotnet test backend/tests/OneCup.UnitTests`
Expected: 全 PASS(NumberingClock 相关测试不受影响,DI 自动解析新位置)。

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat(arch): NumberingClock 迁移到 Application (纯时钟抽象)"
```

---

## Task 8b: 事务抽象(IApplicationTransaction + ExecuteInTransactionAsync)

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/IApplicationTransaction.cs`
- Modify: `backend/src/OneCup.Application/Interfaces/IUnitOfWork.cs`(加 `ExecuteInTransactionAsync`)
- Modify: `backend/src/OneCup.Infrastructure/Persistence/UnitOfWork.cs`(实现,包装 EF `IDbContextTransaction`)
- Modify: `backend/src/OneCup.Infrastructure/Services/NumberingService.cs`(调用方事务守卫改用抽象)
- Test: `backend/tests/OneCup.UnitTests/Persistence/UnitOfWorkTransactionTests.cs`

> NumberingService 要求调用方持事务(否则 fail-fast)。迁移后业务 Service 在 Application 层拿不到 DbContext,需要事务抽象。`IApplicationTransaction` 不暴露 EF 类型;`ExecuteInTransactionAsync` 是最常用的便捷形式(回调内执行,自动提交/回滚)。

- [ ] **Step 1: 定义 IApplicationTransaction + 扩展 IUnitOfWork**

```csharp
// backend/src/OneCup.Application/Interfaces/IApplicationTransaction.cs
namespace OneCup.Application.Interfaces;

/// <summary>
/// 应用层事务抽象,不泄漏 EF Core 的 IDbContextTransaction。
/// </summary>
public interface IApplicationTransaction : IAsyncDisposable
{
    /// <summary>提交事务。</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>回滚事务。</summary>
    Task RollbackAsync(CancellationToken ct = default);
}
```

在 `IUnitOfWork` 加:
```csharp
    /// <summary>在事务中执行操作,自动提交(回调正常)或回滚(回调抛异常)。</summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
```

- [ ] **Step 2: UnitOfWork 实现**

```csharp
// UnitOfWork.cs 增加
public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
{
    // EF Core 会在环境事务内自动为各 SaveChanges 建立 savepoint
    await using var tx = await _db.Database.BeginTransactionAsync(ct);
    try
    {
        await action();
        await tx.CommitAsync(ct);
    }
    catch
    {
        await tx.RollbackAsync(ct);
        throw;
    }
}
```

- [ ] **Step 3: 写测试**

```csharp
// 用 EF InMemory 验证 ExecuteInTransactionAsync 在回调成功时提交、抛异常时回滚(重新执行不报"已有事务")
// 注:InMemory provider 的事务是 no-op,但能验证控制流(提交/回滚不抛二次异常)
```

- [ ] **Step 4: 运行测试**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~UnitOfWorkTransactionTests"`
Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "feat(arch): IApplicationTransaction 事务抽象 (不泄漏 EF 类型)"
```

---

## Task 8c: 迁移 NumberingRuleService(CRUD+分页,left-join)

**Files:**
- Move: `backend/src/OneCup.Infrastructure/Services/NumberingRuleService.cs` → `backend/src/OneCup.Application/Services/NumberingRuleService.cs`
- Create: `backend/src/OneCup.Application/Specifications/NumberingSpecs.cs`
- Modify: `backend/tests/OneCup.UnitTests/System/NumberingRuleServiceTests.cs`(构造改为注入 Repository)

> 与 UserService 同形(GetById + 分页过滤 + AnyAsync 唯一校验)。唯一特殊点:`GetLogsAsync` 的 left-join(规则⧹日志)。由于 left-join 用 LINQ `join ... into ... DefaultIfEmpty` 表达,而当前 Specification 只支持 Where/Include/OrderBy/Paging,**left-join 投影不适合塞进通用 Specification**。
>
> **决策**:`GetLogsAsync` 的 left-join 查询保留为 NumberingRuleService 内的一段 LINQ——但这要求 Service 能拿到 IQueryable,与"Service 不依赖 DbContext"冲突。两个解法:
> - **解法A**:给 IRepository 加一个 `IQueryable<T> Query()` 暴露(最小泄漏,但让复杂查询可表达)。
> - **解法B**:`GetLogsAsync` 单独走一个专用查询接口/Specification,在 Infrastructure 实现该查询,Application 只定义 DTO 与接口签名。
>
> 本 Task 采用**解法A**(给 IRepository 加 `Query()`),因为它能覆盖 left-join 这类投影查询,且 IQueryable 本身不是 EF Core 类型(System.Linq),Application 引用 IQueryablте 不破坏零 EF 约束。Repository 实现 Query() 返回 `_db.Set<T>().AsQueryable()`。

- [ ] **Step 1: 给 IRepository 加 Query()**

在 `IRepository<T>` 加 `IQueryable<T> Query();`,Repository 实现返回 `_db.Set<T>().AsQueryable()`。
> 注:这是"逃生舱口",供 left-join/聚合投影等通用 Specification 难表达的场景。规范用法仍优先 ListAsync(spec)。

- [ ] **Step 2: 定义 NumberingSpecs**

```csharp
// backend/src/OneCup.Application/Specifications/NumberingSpecs.cs
public class NumberingRulePagedSpec : Specification<NumberingRule> { /* keyword/category/status 过滤 + 分页 */ }
public class NumberingRuleByIdSpec : Specification<NumberingRule> { ApplyCriteria(x => x.Id == id); }
public class NumberingRuleByCodeSpec : Specification<NumberingRule> { ApplyCriteria(x => x.Code == code); }
```
(left-join 的 GetLogsAsync 不用 Spec,直接用 Query() + LINQ join)

- [ ] **Step 3: 迁移 NumberingRuleService**

搬到 Application,依赖 `IRepository<NumberingRule>` + `IRepository<NumberingLog>` + `IUnitOfWork` + `INumberingClock`(已上移)。CRUD/分页用 Spec,GetLogsAsync 用 `_logs.Query()` + LINQ left-join。

- [ ] **Step 4: 更新 NumberingRuleServiceTests 构造**

注入 Repository/UoW。用例逻辑不变。

- [ ] **Step 5: 构建并测试**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: 全 PASS。

- [ ] **Step 6: 提交**

```bash
git add -A
git commit -m "feat(arch): NumberingRuleService 迁移到 Application (left-join via Query())"
```

---

## Task 9: 清理 Infrastructure 残留 + DI 收尾

**Files:**
- Modify: `backend/src/OneCup.Api/Program.cs`(确认所有 Service 实现的 using 指向 Application)
- Delete: 确认 `backend/src/OneCup.Infrastructure/Services/` 只剩 `JwtTokenService.cs`、`PasswordHasher.cs`
- Verify: Application 无 EF Core 引用

- [ ] **Step 1: 确认 Infrastructure/Services 剩余文件**

`backend/src/OneCup.Infrastructure/Services/` 应只剩 `JwtTokenService.cs`、`PasswordHasher.cs`、`NumberingService.cs`(技术细节/并发基础设施,留在 Infrastructure)。其余已迁移。

- [ ] **Step 2: 确认 Program.cs using 与 DI**

`using OneCup.Infrastructure.Services;`(JwtTokenService/PasswordHasher 仍需)+ `using OneCup.Application.Services;`(其余 Service)。DI 注册 `AddScoped<IXxx, Xxx>()` 具体类型自动从新命名空间解析。

- [ ] **Step 3: 验证 Application 零 EF Core**

Run: `dotnet list backend/src/OneCup.Application package`
Expected: 输出仅 `Microsoft.Extensions.Options`(无 EFCore/EntityFramework)。

- [ ] **Step 4: 全量构建+测试**

Run: `dotnet build backend/src/OneCup.Api/OneCup.Api.csproj && dotnet test backend/tests/OneCup.UnitTests`
Expected: 构建成功,全 PASS(51+,迁移不丢测试)。

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "chore(arch): 阶段B 收尾 — DI using 清理 + 验证 Application 零 EF Core"
```

---

## Task 10: 阶段 B 冒烟验证

**Files:** 无

- [ ] **Step 1: 全量测试**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: 全 PASS。

- [ ] **Step 2: 架构纯净度验证**

Run: `dotnet list backend/src/OneCup.Application package`
Expected: 仅 `Microsoft.Extensions.Options`。

Run(grep 验证 Application 无 EF 类型):
```bash
grep -r "Microsoft.EntityFrameworkCore\|DbContext\|DbSet" backend/src/OneCup.Application/ && echo "FOUND EF (BAD)" || echo "CLEAN"
```
Expected: CLEAN。

- [ ] **Step 3: 启动冒烟(可选,需数据库)**

Run: `cd backend/src/OneCup.Api && dotnet run`(配好 SecretKey + DB)
Expected: 启动成功,登录/用户管理/角色管理 API 正常(行为与阶段A一致)。

- [ ] **Step 4: 提交里程碑**

```bash
git commit --allow-empty -m "chore(arch): 阶段B 冒烟验证通过 — Application 零 EF Core, 全测试通过"
```

---

## Self-Review(写作后自检)

**1. Spec 覆盖(spec 第 4 节 B 架构修正):**
- 4.2 Specification 模式 → Task 1+2 ✓
- 4.3 Service 迁移(User/Role/Permission/Auth)→ Task 5/6/7/8 ✓
- 4.4 UnitOfWork 真正启用 → Task 6/7/8 改 SaveChangesAsync 走 UoW ✓
- 4.5 迁移影响(测试改写)→ 各 Task 测试改写 ✓
- JwtTokenService/PasswordHasher 留 Infrastructure(spec 4.3 注)→ Task 9 确认 ✓

**2. Includes 设计(字符串路径):** Task 1 起 `Includes` 即为 `IReadOnlyList<string>`,支持点分多级路径(如 `"Roles.Permissions"`)。EF Core `Include(string)` 原生支持,无需 ThenInclude 表达式树,Application 层零 EF 类型依赖。各 Spec(Task 6 UserSpecs、Task 7 RoleSpecs、Task 8 AuthSpecs)统一使用 `ApplyInclude("路径")`。计划内无中途演进、无矛盾。✓

**3. 类型一致性:** ISpecification 在各 Task 引用一致;SystemConstants 在 Task 3 定义后被 Task 6/7 引用;ILockoutStore Task 4 提升后被 Task 8 引用。✓

**4. 测试连贯性:** 各 Service 测试构造函数随迁移演进,每 Task Step 都更新对应测试。✓
