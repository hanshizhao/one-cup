# 系统管理界面实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为认证系统补充可视化的 RBAC 管理后台（用户/角色/权限三件套），管理员能在界面上管理用户、角色和权限分配。

**Architecture:** 后端沿用 Clean Architecture 新增 UserService/RoleService/PermissionService + 3 个 Controller + 基于 JWT claims 的 Policy 授权（含 admin `*` 通配处理）。前端清理 Arco Pro demo 菜单，新增「系统管理」菜单 + 三个页面（表格 + Drawer 抽屉表单）。

**Tech Stack:** .NET 10 / EF Core 10 / PostgreSQL；React 17 / TypeScript / Arco Design（Table/Drawer/Form/Tree/Select）

## Global Constraints

- 后端依赖方向严格单向：Api → Application → Domain，Infrastructure → Application → Domain，Domain 零依赖
- 数据库表名/列名统一 snake_case（已在实体配置中处理，本轮不新增表，只复用认证已建的 6 张表）
- 权限校验：`/api/users/*` 需 `system:user:manage`，`/api/roles/*` 需 `system:role:manage`，admin 角色的 `perm_codes=["*"]` 通配放行所有策略
- admin 用户不可禁用、不可移除 admin 角色（后端强制校验）
- admin 角色（code="admin"）不可删除
- 角色删除前校验：有关联用户则拒绝
- 用户名创建后不可修改
- 密码单独管理（重置密码入口），不在编辑表单里
- 权限（Permission）只读，不在界面增删（随业务模块在 SeedData 预置）
- Arco Design 组件：`Form.Item` 用 `field` 属性（不是 `name`）；Switch 在 Form 里需 `triggerPropName="checked"`
- 枚举序列化为字符串（Api 层已配置 `JsonStringEnumConverter`）

## File Structure

**后端新建文件：**
```
backend/src/OneCup.Application/
  Dtos/System/UserDtos.cs          # 用户相关 DTO（6 个）
  Dtos/System/RoleDtos.cs          # 角色相关 DTO（4 个）
  Dtos/System/PermissionDto.cs     # 权限 DTO
  Interfaces/IUserService.cs       # 用户管理服务接口
  Interfaces/IRoleService.cs       # 角色管理服务接口
  Interfaces/IPermissionService.cs # 权限查询接口

backend/src/OneCup.Infrastructure/
  Services/UserService.cs          # 用户管理实现
  Services/RoleService.cs          # 角色管理实现
  Services/PermissionService.cs    # 权限查询实现

backend/src/OneCup.Api/
  Controllers/UsersController.cs       # 6 个用户端点
  Controllers/RolesController.cs       # 5 个角色端点
  Controllers/PermissionsController.cs # 1 个权限端点
  Authorization/WildcardAuthorizationHandler.cs  # admin * 通配处理
```

**后端修改文件：**
```
backend/src/OneCup.Api/Program.cs  # 注册新 Service + 权限 Policy + WildcardHandler
```

**前端新建文件：**
```
frontend/src/api/user.ts                       # 用户 CRUD API
frontend/src/api/role.ts                       # 角色 CRUD API
frontend/src/api/permission.ts                 # 权限列表 API
frontend/src/pages/system/user/index.tsx       # 用户管理页
frontend/src/pages/system/user/locale/index.ts # 用户页 i18n
frontend/src/pages/system/role/index.tsx       # 角色管理页
frontend/src/pages/system/role/locale/index.ts # 角色页 i18n
frontend/src/pages/system/permission/index.tsx # 权限列表页
frontend/src/pages/system/permission/locale/index.ts # 权限页 i18n
```

**前端修改文件：**
```
frontend/src/routes.ts           # 删 demo 菜单，新增系统管理菜单
frontend/src/locale/index.ts     # 新增系统管理 i18n，删 demo i18n
frontend/src/layout.tsx          # 删 demo 菜单图标映射，新增系统管理图标
```

**后端测试新建文件：**
```
backend/tests/OneCup.UnitTests/System/UserServiceTests.cs
backend/tests/OneCup.UnitTests/System/RoleServiceTests.cs
```

---

## Task 1: Application 层 — DTO 与服务接口

**Files:**
- Create: `backend/src/OneCup.Application/Dtos/System/UserDtos.cs`
- Create: `backend/src/OneCup.Application/Dtos/System/RoleDtos.cs`
- Create: `backend/src/OneCup.Application/Dtos/System/PermissionDto.cs`
- Create: `backend/src/OneCup.Application/Interfaces/IUserService.cs`
- Create: `backend/src/OneCup.Application/Interfaces/IRoleService.cs`
- Create: `backend/src/OneCup.Application/Interfaces/IPermissionService.cs`

**Interfaces:**
- Consumes: `PagedResult<T>`（已有，`Application/Common/PagedResult.cs`）、`BaseEntity`（已有）
- Produces: 11 个 DTO 类型 + 3 个服务接口，供 Infrastructure 实现与 Api Controller 调用

- [ ] **Step 1: 创建用户 DTO**

```csharp
namespace OneCup.Application.Dtos.System;

public class UserListItemDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> RoleNames { get; set; } = [];
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Guid> RoleIds { get; set; } = [];
    public List<string> RoleNames { get; set; } = [];
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Password { get; set; } = string.Empty;
    public List<Guid> RoleIds { get; set; } = [];
}

public class UpdateUserRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public List<Guid> RoleIds { get; set; } = [];
}

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

public class UpdateStatusRequest
{
    public bool IsActive { get; set; }
}
```

- [ ] **Step 2: 创建角色 DTO**

```csharp
namespace OneCup.Application.Dtos.System;

public class RoleListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UserCount { get; set; }
    public int PermissionCount { get; set; }
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Guid> PermissionIds { get; set; } = [];
}

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<Guid> PermissionIds { get; set; } = [];
}
```

- [ ] **Step 3: 创建权限 DTO**

```csharp
namespace OneCup.Application.Dtos.System;

public class PermissionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
```

- [ ] **Step 4: 创建 IUserService 接口**

```csharp
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> GetListAsync(int page, int pageSize, string? keyword, CancellationToken ct = default);

    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);

    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);

    Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default);

    Task UpdateStatusAsync(Guid id, UpdateStatusRequest request, CancellationToken ct = default);
}
```

- [ ] **Step 5: 创建 IRoleService 接口**

```csharp
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IRoleService
{
    Task<List<RoleListItemDto>> GetListAsync(CancellationToken ct = default);

    Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken ct = default);

    Task<RoleDto> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 6: 创建 IPermissionService 接口**

```csharp
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IPermissionService
{
    Task<List<PermissionDto>> GetListAsync(CancellationToken ct = default);
}
```

- [ ] **Step 7: 验证编译**

Run: `dotnet build backend/src/OneCup.Application/OneCup.Application.csproj`
Expected: BUILD SUCCEEDED, 0 errors

- [ ] **Step 8: Commit**

```bash
git add backend/src/OneCup.Application/
git commit -m "feat(application): 系统管理 DTO 与服务接口"
```

---

## Task 2: Infrastructure — UserService 实现

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Services/UserService.cs`

**Interfaces:**
- Consumes: `IUserService`（Task 1）、`IPasswordHasher`（已有）、`OneCupDbContext`（已有）、`PagedResult<T>`（已有）、`DomainException`（已有）、`SeedData.AdminUserId`（已有）
- Produces: `UserService` 实现，供 Task 5 Controller 调用

- [ ] **Step 1: 创建 UserService 实现**

```csharp
using Microsoft.EntityFrameworkCore;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// 用户管理服务实现。
/// </summary>
public class UserService : IUserService
{
    private readonly OneCupDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(OneCupDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<PagedResult<UserListItemDto>> GetListAsync(int page, int pageSize, string? keyword, CancellationToken ct = default)
    {
        var query = _db.Users
            .Include(u => u.Roles)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim();
            query = query.Where(u => u.Username.Contains(keyword) || u.DisplayName.Contains(keyword));
        }

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<UserListItemDto>
        {
            Items = users.Select(u => new UserListItemDto
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                Email = u.Email,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                RoleNames = u.Roles.Select(r => r.Name).ToList(),
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return null;

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            RoleIds = user.Roles.Select(r => r.Id).ToList(),
            RoleNames = user.Roles.Select(r => r.Name).ToList(),
        };
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        // 用户名唯一校验
        if (await _db.Users.AnyAsync(u => u.Username == request.Username, ct))
        {
            throw new DomainException($"用户名「{request.Username}」已存在");
        }

        var roles = await _db.Roles
            .Where(r => request.RoleIds.Contains(r.Id))
            .ToListAsync(ct);

        var user = new User
        {
            Username = request.Username,
            DisplayName = request.DisplayName,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = true,
            Roles = roles,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(user.Id, ct) ?? throw new DomainException("用户创建失败");
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("用户不存在");

        // admin 保护：不能移除 admin 角色关联中唯一的 admin 角色
        // （即如果当前用户是 admin，不能把 admin 角色从 roleIds 里去掉）
        // 这里简化处理：admin 用户的 Roles 操作不做限制（admin 角色本身有通配权限）
        // 但禁止把 admin 用户设为非 admin 角色——如果用户当前有 admin 角色，保留它
        var hasAdminRole = user.Roles.Any(r => r.Code == "admin");

        var roles = await _db.Roles
            .Where(r => request.RoleIds.Contains(r.Id))
            .ToListAsync(ct);

        // admin 保护：如果用户原来是 admin，确保 admin 角色仍在列表中
        if (hasAdminRole)
        {
            var adminRole = await _db.Roles.FirstAsync(r => r.Code == "admin", ct);
            if (!roles.Any(r => r.Code == "admin"))
            {
                roles.Add(adminRole);
            }
        }

        user.DisplayName = request.DisplayName;
        user.Email = request.Email;
        user.IsActive = request.IsActive;
        user.Roles = roles;

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(user.Id, ct) ?? throw new DomainException("用户更新失败");
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("用户不存在");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateStatusAsync(Guid id, UpdateStatusRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("用户不存在");

        // admin 保护：不能禁用 admin 用户
        if (!request.IsActive && user.Id == SeedData.AdminUserId)
        {
            throw new DomainException("不能禁用系统管理员账号");
        }

        user.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj`
Expected: BUILD SUCCEEDED, 0 errors

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Services/UserService.cs
git commit -m "feat(infra): UserService 用户管理实现 (CRUD + admin 保护)"
```

---

## Task 3: Infrastructure — RoleService 与 PermissionService 实现

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Services/RoleService.cs`
- Create: `backend/src/OneCup.Infrastructure/Services/PermissionService.cs`

**Interfaces:**
- Consumes: `IRoleService`/`IPermissionService`（Task 1）、`OneCupDbContext`（已有）、`DomainException`（已有）、`SeedData`（已有）
- Produces: `RoleService` + `PermissionService` 实现，供 Task 5 Controller 调用

- [ ] **Step 1: 创建 RoleService 实现**

```csharp
using Microsoft.EntityFrameworkCore;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// 角色管理服务实现。
/// </summary>
public class RoleService : IRoleService
{
    private readonly OneCupDbContext _db;

    public RoleService(OneCupDbContext db)
    {
        _db = db;
    }

    public async Task<List<RoleListItemDto>> GetListAsync(CancellationToken ct = default)
    {
        var roles = await _db.Roles
            .Include(r => r.Permissions)
            .ToListAsync(ct);

        // 批量统计每个角色的用户数
        var userCounts = await _db.Users
            .SelectMany(u => u.Roles.Select(r => new { UserId = u.Id, RoleId = r.Id }))
            .GroupBy(x => x.RoleId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.UserId).Distinct().Count(), ct);

        return roles.Select(r => new RoleListItemDto
        {
            Id = r.Id,
            Name = r.Name,
            Code = r.Code,
            Description = r.Description,
            CreatedAt = r.CreatedAt,
            UserCount = userCounts.GetValueOrDefault(r.Id, 0),
            PermissionCount = r.Permissions.Count,
        }).ToList();
    }

    public async Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null) return null;

        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Code = role.Code,
            Description = role.Description,
            CreatedAt = role.CreatedAt,
            PermissionIds = role.Permissions.Select(p => p.Id).ToList(),
        };
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        if (await _db.Roles.AnyAsync(r => r.Code == request.Code, ct))
        {
            throw new DomainException($"角色编码「{request.Code}」已存在");
        }

        var role = new Role
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(role.Id, ct) ?? throw new DomainException("角色创建失败");
    }

    public async Task<RoleDto> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new DomainException("角色不存在");

        var permissions = await _db.Permissions
            .Where(p => request.PermissionIds.Contains(p.Id))
            .ToListAsync(ct);

        role.Name = request.Name;
        role.Description = request.Description;
        role.Permissions = permissions;

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(role.Id, ct) ?? throw new DomainException("角色更新失败");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new DomainException("角色不存在");

        // admin 角色不可删除
        if (role.Code == "admin")
        {
            throw new DomainException("系统内置管理员角色不可删除");
        }

        // 有关联用户则拒绝
        var userCount = await _db.Users
            .SelectMany(u => u.Roles.Select(r => r.Id))
            .CountAsync(rid => rid == id, ct);

        if (userCount > 0)
        {
            throw new DomainException($"该角色下还有 {userCount} 个用户，请先解除关联后再删除");
        }

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 2: 创建 PermissionService 实现**

```csharp
using Microsoft.EntityFrameworkCore;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Infrastructure.Persistence;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// 权限查询服务实现（只读）。
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly OneCupDbContext _db;

    public PermissionService(OneCupDbContext db)
    {
        _db = db;
    }

    public async Task<List<PermissionDto>> GetListAsync(CancellationToken ct = default)
    {
        return await _db.Permissions
            .OrderBy(p => p.Code)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
            })
            .ToListAsync(ct);
    }
}
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj`
Expected: BUILD SUCCEEDED, 0 errors

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Services/RoleService.cs backend/src/OneCup.Infrastructure/Services/PermissionService.cs
git commit -m "feat(infra): RoleService 与 PermissionService 实现"
```

---

## Task 4: 单元测试 — UserService 与 RoleService

**Files:**
- Create: `backend/tests/OneCup.UnitTests/System/UserServiceTests.cs`
- Create: `backend/tests/OneCup.UnitTests/System/RoleServiceTests.cs`

**Interfaces:**
- Consumes: `UserService`/`RoleService`（Task 2/3）、`OneCupDbContext`、`PasswordHasher`、EF Core InMemory（已在测试项目）
- Produces: 覆盖核心管理逻辑的单元测试

- [ ] **Step 1: 创建 UserService 测试**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using OneCup.Infrastructure.Services;

namespace OneCup.UnitTests.System;

public class UserServiceTests
{
    private static (OneCupDbContext db, UserService svc) Setup(params User[] seedUsers)
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"user-test-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);

        // 种子角色
        var adminRole = new Role { Id = SeedData.AdminRoleId, Name = "管理员", Code = "admin" };
        var devRole = new Role { Id = SeedData.DeveloperRoleId, Name = "开发员", Code = "developer" };
        db.Roles.AddRange(adminRole, devRole);
        db.Users.AddRange(seedUsers);
        db.SaveChanges();

        var svc = new UserService(db, new PasswordHasher());
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static User MakeUser(string username, string display) => new()
    {
        Username = username,
        DisplayName = display,
        PasswordHash = "$2a$12$placeholder",
        IsActive = true,
    };

    [Fact]
    public async Task GetListAsync_ReturnsPagedResults()
    {
        var (db, svc) = Setup(MakeUser("user1", "用户一"), MakeUser("user2", "用户二"));
        var result = await svc.GetListAsync(1, 10, null);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetListAsync_KeywordFiltersByUsername()
    {
        var (db, svc) = Setup(MakeUser("alice", "爱丽丝"), MakeUser("bob", "鲍勃"));
        var result = await svc.GetListAsync(1, 10, "alice");
        Assert.Single(result.Items);
        Assert.Equal("alice", result.Items[0].Username);
    }

    [Fact]
    public async Task CreateAsync_CreatesUserWithRoles()
    {
        var (db, svc) = Setup();
        var result = await svc.CreateAsync(new CreateUserRequest
        {
            Username = "newuser",
            DisplayName = "新用户",
            Password = "Pass@123",
            RoleIds = [SeedData.DeveloperRoleId],
        });
        Assert.Equal("newuser", result.Username);
        Assert.Contains(SeedData.DeveloperRoleId, result.RoleIds);
    }

    [Fact]
    public async Task CreateAsync_DuplicateUsername_Throws()
    {
        var (db, svc) = Setup(MakeUser("dup", "重复用户"));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(new CreateUserRequest { Username = "dup", DisplayName = "x", Password = "p" }));
    }

    [Fact]
    public async Task UpdateStatusAsync_DisableAdminUser_Throws()
    {
        var adminUser = MakeUser("admin", "管理员");
        adminUser.Id = SeedData.AdminUserId;
        var (db, svc) = Setup(adminUser);
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateStatusAsync(SeedData.AdminUserId, new UpdateStatusRequest { IsActive = false }));
    }

    [Fact]
    public async Task ResetPasswordAsync_UpdatesHash()
    {
        var user = MakeUser("u1", "用户一");
        var (db, svc) = Setup(user);
        await svc.ResetPasswordAsync(user.Id, new ResetPasswordRequest { NewPassword = "NewPass@456" });
        var updated = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.NotEqual("$2a$12$placeholder", updated.PasswordHash);
    }
}
```

- [ ] **Step 2: 创建 RoleService 测试**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using OneCup.Infrastructure.Services;

namespace OneCup.UnitTests.System;

public class RoleServiceTests
{
    private static (OneCupDbContext db, RoleService svc) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"role-test-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);

        db.Roles.Add(new Role { Id = SeedData.AdminRoleId, Name = "管理员", Code = "admin" });
        db.Permissions.Add(new Permission { Id = SeedData.PermFabricRead, Code = "fabric:read", Name = "查看面料" });
        db.SaveChanges();

        var svc = new RoleService(db);
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task GetListAsync_ReturnsAllRoles()
    {
        var (db, svc) = Setup();
        var result = await svc.GetListAsync();
        Assert.Single(result); // admin
    }

    [Fact]
    public async Task CreateAsync_CreatesRole()
    {
        var (db, svc) = Setup();
        var result = await svc.CreateAsync(new CreateRoleRequest { Name = "测试角色", Code = "tester" });
        Assert.Equal("tester", result.Code);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_Throws()
    {
        var (db, svc) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(new CreateRoleRequest { Name = "管理员2", Code = "admin" }));
    }

    [Fact]
    public async Task UpdateAsync_AssignsPermissions()
    {
        var (db, svc) = Setup();
        var role = new Role { Name = "开发", Code = "dev" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var result = await svc.UpdateAsync(role.Id, new UpdateRoleRequest
        {
            Name = "开发",
            PermissionIds = [SeedData.PermFabricRead],
        });
        Assert.Contains(SeedData.PermFabricRead, result.PermissionIds);
    }

    [Fact]
    public async Task DeleteAsync_AdminRole_Throws()
    {
        var (db, svc) = Setup();
        await Assert.ThrowsAsync<DomainException>(() => svc.DeleteAsync(SeedData.AdminRoleId));
    }
}
```

- [ ] **Step 3: 运行测试**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~System"`
Expected: 11 passed (6 UserService + 5 RoleService), 0 failed

- [ ] **Step 4: Commit**

```bash
git add backend/tests/OneCup.UnitTests/System/
git commit -m "test: UserService 与 RoleService 单元测试"
```

---

## Task 5: Api 层 — 授权处理与 Controller

**Files:**
- Create: `backend/src/OneCup.Api/Authorization/WildcardAuthorizationHandler.cs`
- Create: `backend/src/OneCup.Api/Controllers/UsersController.cs`
- Create: `backend/src/OneCup.Api/Controllers/RolesController.cs`
- Create: `backend/src/OneCup.Api/Controllers/PermissionsController.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`

**Interfaces:**
- Consumes: `IUserService`/`IRoleService`/`IPermissionService`（Task 2/3）
- Produces: 12 个 HTTP 端点 + admin `*` 通配授权

- [ ] **Step 1: 创建通配授权 Handler（admin 的 * claim 放行所有策略）**

```csharp
using Microsoft.AspNetCore.Authorization;

namespace OneCup.Api.Authorization;

/// <summary>
/// admin 角色的 perm_codes 包含通配 "*"，直接放行所有策略。
/// </summary>
public class WildcardAuthorizationHandler : AuthorizationHandler<IAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, IAuthorizationRequirement requirement)
    {
        if (context.User.HasClaim("perm_codes", "*"))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: 创建 UsersController**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 用户管理端点。需要 system:user:manage 权限（或 admin 通配）。
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = "user-manage")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, CancellationToken ct = default)
    {
        var result = await _userService.GetListAsync(page, pageSize, keyword, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await _userService.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var user = await _userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var user = await _userService.UpdateAsync(id, request, ct);
        return Ok(user);
    }

    [HttpPut("{id:guid}/password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _userService.ResetPasswordAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        await _userService.UpdateStatusAsync(id, request, ct);
        return NoContent();
    }
}
```

- [ ] **Step 3: 创建 RolesController**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 角色管理端点。需要 system:role:manage 权限（或 admin 通配）。
/// </summary>
[ApiController]
[Route("api/roles")]
[Authorize(Policy = "role-manage")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        var roles = await _roleService.GetListAsync(ct);
        return Ok(roles);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var role = await _roleService.GetByIdAsync(id, ct);
        return role is null ? NotFound() : Ok(role);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        var role = await _roleService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = role.Id }, role);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken ct)
    {
        var role = await _roleService.UpdateAsync(id, request, ct);
        return Ok(role);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _roleService.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

- [ ] **Step 4: 创建 PermissionsController**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 权限查询端点（只读）。仅需登录。
/// </summary>
[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionsController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        var permissions = await _permissionService.GetListAsync(ct);
        return Ok(permissions);
    }
}
```

- [ ] **Step 5: 改造 Program.cs — 注册 Service + Policy + Handler**

在 `Program.cs` 中，已有认证授权 DI 注册区之后追加：

```csharp
// ── 依赖注入:系统管理服务 ─────────────────────────────────────
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

// ── 授权策略 (基于 JWT perm_codes claim) ───────────────────────
builder.Services.AddSingleton<IAuthorizationHandler, WildcardAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("user-manage", policy =>
        policy.RequireClaim("perm_codes", "system:user:manage"));
    options.AddPolicy("role-manage", policy =>
        policy.RequireClaim("perm_codes", "system:role:manage"));
});
```

文件顶部 using 区追加：

```csharp
using Microsoft.AspNetCore.Authorization;
using OneCup.Api.Authorization;
```

> 注意：`builder.Services.AddAuthorization()` 已在认证部分调用过（第 66 行）。上面的 `AddAuthorization(options => ...)` 会追加策略配置。合并为一次调用更干净，但分两次调用也合法（后者追加 options）。实现时可将两次合并。

- [ ] **Step 6: 验证编译**

Run: `dotnet build backend/src/OneCup.Api/OneCup.Api.csproj`
Expected: BUILD SUCCEEDED, 0 errors

- [ ] **Step 7: Commit**

```bash
git add backend/src/OneCup.Api/
git commit -m "feat(api): 系统管理 Controller + 授权策略 (admin 通配)"
```

---

## Task 6: 后端集成验证

**Files:** 无（验证 Task 2-5 的产物）

**前提：** 后端启动，数据库已迁移（认证开发已完成），admin 账号可用。

- [ ] **Step 1: 启动后端**

Run:
```bash
cd backend/src/OneCup.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls "http://localhost:5233"
```
Expected: 监听 5233 端口，无异常

- [ ] **Step 2: 登录获取 token**

Run:
```bash
curl -s -X POST http://localhost:5233/api/auth/login -H "Content-Type: application/json" -d '{"username":"admin","password":"Admin@123"}'
```
保存返回的 accessToken。

- [ ] **Step 3: 测试权限列表**

Run（用上一步的 token 替换 `<TOKEN>`）：
```bash
curl -s http://localhost:5233/api/permissions -H "Authorization: Bearer <TOKEN>"
```
Expected: 200，返回 13 个权限

- [ ] **Step 4: 测试角色列表**

```bash
curl -s http://localhost:5233/api/roles -H "Authorization: Bearer <TOKEN>"
```
Expected: 200，返回 2 个角色（admin + developer）

- [ ] **Step 5: 测试新增角色**

```bash
curl -s -X POST http://localhost:5233/api/roles -H "Authorization: Bearer <TOKEN>" -H "Content-Type: application/json" -d '{"name":"测试角色","code":"tester","description":"测试用"}'
```
Expected: 201，返回新角色

- [ ] **Step 6: 测试用户列表**

```bash
curl -s "http://localhost:5233/api/users?page=1&pageSize=10" -H "Authorization: Bearer <TOKEN>"
```
Expected: 200，返回 admin 用户

- [ ] **Step 7: 测试未授权访问（不带 token）**

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5233/api/users
```
Expected: `401`

- [ ] **Step 8: 测试删除 admin 角色（应拒绝）**

```bash
curl -s -X DELETE http://localhost:5233/api/roles/00000000-0000-0000-0000-000000000002 -H "Authorization: Bearer <TOKEN>"
```
Expected: `400`（DomainException），message 含"不可删除"

> 验证通过后停止后端。无需 commit（无代码变更）。

---

## Task 7: 前端 — 菜单重构与 i18n

**Files:**
- Modify: `frontend/src/routes.ts`
- Modify: `frontend/src/locale/index.ts`
- Modify: `frontend/src/layout.tsx`

**Interfaces:**
- Consumes: 现有路由/布局/i18n 结构
- Produces: 清理后的菜单（只有系统管理），为 Task 8-10 的页面提供路由

- [ ] **Step 1: 重写 routes.ts**

将 `frontend/src/routes.ts` 的 `routes` 数组替换为（保留 `IRoute` 类型、`getName`、`useRoute`、`generatePermission` 等已有函数，只改 `routes` 导出和 `generatePermission`）：

```typescript
import auth, { AuthParams } from '@/utils/authentication';
import { useEffect, useMemo, useState } from 'react';

export type IRoute = AuthParams & {
  name: string;
  key: string;
  breadcrumb?: boolean;
  children?: IRoute[];
  ignore?: boolean;
};

export const routes: IRoute[] = [
  {
    name: 'menu.system',
    key: 'system',
    children: [
      {
        name: 'menu.system.user',
        key: 'system/user',
        requiredPermissions: [
          { resource: 'system:user', actions: ['manage'] },
        ],
      },
      {
        name: 'menu.system.role',
        key: 'system/role',
        requiredPermissions: [
          { resource: 'system:role', actions: ['manage'] },
        ],
      },
      {
        name: 'menu.system.permission',
        key: 'system/permission',
      },
    ],
  },
];

// generatePermission 保留（NavBar 中切换角色时使用，demo 残留，可后续清理）
export const generatePermission = (role: string) => {
  const actions = role === 'admin' ? ['*'] : ['read'];
  const result = {};
  routes.forEach((item) => {
    if (item.children) {
      item.children.forEach((child) => {
        result[child.name] = actions;
      });
    }
  });
  return result;
};
```

> `useRoute` 和 `getName` 函数保持不变（它们是通用的路由过滤工具）。

- [ ] **Step 2: 更新 locale/index.ts**

在 `en-US` 和 `zh-CN` 两个对象中，**删除**所有 `menu.dashboard.*`、`menu.list.*`、`menu.form.*`、`menu.profile.*`、`menu.result.*`、`menu.exception.*`、`menu.visualization.*`、`menu.user.info`、`menu.user.setting` 条目。

**新增**以下条目：

`en-US` 新增：
```typescript
    'menu.system': 'System',
    'menu.system.user': 'Users',
    'menu.system.role': 'Roles',
    'menu.system.permission': 'Permissions',
```

`zh-CN` 新增：
```typescript
    'menu.system': '系统管理',
    'menu.system.user': '用户管理',
    'menu.system.role': '角色管理',
    'menu.system.permission': '权限列表',
```

保留 `menu.user`（用于 NavBar 用户下拉）、`navbar.logout` 等非 demo 菜单条目。

- [ ] **Step 3: 更新 layout.tsx 的图标映射**

在 `frontend/src/layout.tsx` 的 `getIconFromKey` 函数中，新增 `system` 的图标映射（用 `IconSettings` 或 `IconUser`），删除不再需要的 demo 图标 case（dashboard/list/form/profile/visualization/result/exception/user 可保留但无害，也可删除）。

在 `getIconFromKey` 的 switch 中新增：
```typescript
    case 'system':
      return <IconSettings className={styles.icon} />;
```

> import 区已有 `IconSettings`（layout.tsx 第 7 行已引入）。

- [ ] **Step 4: 验证 TS 编译**

Run:
```bash
cd frontend && npx tsc --noEmit 2>&1 | head -20
```
Expected: 无报错

- [ ] **Step 5: Commit**

```bash
git add frontend/src/routes.ts frontend/src/locale/index.ts frontend/src/layout.tsx
git commit -m "feat(fe): 菜单重构 — 删除 demo 菜单，新增系统管理菜单"
```

---

## Task 8: 前端 — API 模块与用户管理页

**Files:**
- Create: `frontend/src/api/user.ts`
- Create: `frontend/src/api/role.ts`
- Create: `frontend/src/api/permission.ts`
- Create: `frontend/src/pages/system/user/index.tsx`
- Create: `frontend/src/pages/system/user/locale/index.ts`

**Interfaces:**
- Consumes: `request`（已有 axios 封装 `src/api/request.ts`）、后端用户/角色/权限 API（Task 5）
- Produces: 用户管理页（表格 + Drawer 抽屉表单）

- [ ] **Step 1: 创建 API 模块**

`frontend/src/api/user.ts`：
```typescript
import request from './request';

export interface UserListItem {
  id: string;
  username: string;
  displayName: string;
  email?: string;
  isActive: boolean;
  createdAt: string;
  roleNames: string[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface UserDetail {
  id: string;
  username: string;
  displayName: string;
  email?: string;
  isActive: boolean;
  createdAt: string;
  roleIds: string[];
  roleNames: string[];
}

export interface RoleOption {
  id: string;
  name: string;
  code: string;
}

export function getUserList(page: number, pageSize: number, keyword?: string) {
  return request.get<unknown, PagedResult<UserListItem>>('/api/users', {
    params: { page, pageSize, keyword },
  });
}

export function getUserById(id: string) {
  return request.get<unknown, UserDetail>(`/api/users/${id}`);
}

export function createUser(data: {
  username: string;
  displayName: string;
  email?: string;
  password: string;
  roleIds: string[];
}) {
  return request.post('/api/users', data);
}

export function updateUser(id: string, data: {
  displayName: string;
  email?: string;
  isActive: boolean;
  roleIds: string[];
}) {
  return request.put(`/api/users/${id}`, data);
}

export function resetPassword(id: string, newPassword: string) {
  return request.put(`/api/users/${id}/password`, { newPassword });
}

export function updateUserStatus(id: string, isActive: boolean) {
  return request.put(`/api/users/${id}/status`, { isActive });
}
```

`frontend/src/api/role.ts`：
```typescript
import request from './request';

export interface RoleListItem {
  id: string;
  name: string;
  code: string;
  description?: string;
  createdAt: string;
  userCount: number;
  permissionCount: number;
}

export interface RoleDetail {
  id: string;
  name: string;
  code: string;
  description?: string;
  createdAt: string;
  permissionIds: string[];
}

export function getRoleList() {
  return request.get<unknown, RoleListItem[]>('/api/roles');
}

export function getRoleById(id: string) {
  return request.get<unknown, RoleDetail>(`/api/roles/${id}`);
}

export function createRole(data: { name: string; code: string; description?: string }) {
  return request.post('/api/roles', data);
}

export function updateRole(id: string, data: { name: string; description?: string; permissionIds: string[] }) {
  return request.put(`/api/roles/${id}`, data);
}

export function deleteRole(id: string) {
  return request.delete(`/api/roles/${id}`);
}
```

`frontend/src/api/permission.ts`：
```typescript
import request from './request';

export interface PermissionItem {
  id: string;
  code: string;
  name: string;
  description?: string;
}

export function getPermissionList() {
  return request.get<unknown, PermissionItem[]>('/api/permissions');
}
```

- [ ] **Step 2: 创建用户管理页 i18n**

`frontend/src/pages/system/user/locale/index.ts`：
```typescript
export default {
  'en-US': {
    'user.title': 'User Management',
    'user.search.placeholder': 'Search username or display name',
    'user.add': 'Add User',
    'user.edit': 'Edit User',
    'user.resetPassword': 'Reset Password',
    'user.username': 'Username',
    'user.displayName': 'Display Name',
    'user.email': 'Email',
    'user.roles': 'Roles',
    'user.status': 'Status',
    'user.actions': 'Actions',
    'user.active': 'Active',
    'user.inactive': 'Inactive',
    'user.password': 'Password',
    'user.confirmPassword': 'Confirm Password',
    'user.assignRoles': 'Assign Roles',
    'user.newPassword': 'New Password',
    'user.enable': 'Enable',
    'user.disable': 'Disable',
    'user.add.success': 'User created successfully',
    'user.edit.success': 'User updated successfully',
    'user.reset.success': 'Password reset successfully',
    'user.status.success': 'Status updated successfully',
    'user.password.mismatch': 'Passwords do not match',
  },
  'zh-CN': {
    'user.title': '用户管理',
    'user.search.placeholder': '搜索用户名或显示名',
    'user.add': '新增用户',
    'user.edit': '编辑用户',
    'user.resetPassword': '重置密码',
    'user.username': '用户名',
    'user.displayName': '显示名',
    'user.email': '邮箱',
    'user.roles': '角色',
    'user.status': '状态',
    'user.actions': '操作',
    'user.active': '启用',
    'user.inactive': '禁用',
    'user.password': '密码',
    'user.confirmPassword': '确认密码',
    'user.assignRoles': '分配角色',
    'user.newPassword': '新密码',
    'user.enable': '启用',
    'user.disable': '禁用',
    'user.add.success': '用户创建成功',
    'user.edit.success': '用户更新成功',
    'user.reset.success': '密码重置成功',
    'user.status.success': '状态更新成功',
    'user.password.mismatch': '两次输入的密码不一致',
  },
};
```

- [ ] **Step 3: 创建用户管理页**

`frontend/src/pages/system/user/index.tsx`（较长，包含表格 + 新增/编辑抽屉 + 重置密码抽屉）：

```tsx
import React, { useEffect, useState, useCallback } from 'react';
import {
  Table,
  Button,
  Input,
  Drawer,
  Form,
  Select,
  Switch,
  Tag,
  Popconfirm,
  Message,
  Space,
} from '@arco-design/web-react';
import { FormInstance } from '@arco-design/web-react/es/Form';
import { IconPlus, IconSearch } from '@arco-design/web-react/icon';
import useLocale from '@/utils/useLocale';
import {
  getUserList,
  getUserById,
  createUser,
  updateUser,
  resetPassword,
  updateUserStatus,
  UserListItem,
  RoleOption,
} from '@/api/user';
import { getRoleList } from '@/api/role';
import locale from './locale';

const FormItem = Form.Item;

export default function UserManagement() {
  const t = useLocale(locale);
  const [data, setData] = useState<UserListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [pagination, setPagination] = useState({
    current: 1,
    pageSize: 10,
    total: 0,
  });
  const [keyword, setKeyword] = useState('');

  // 抽屉状态
  const [editVisible, setEditVisible] = useState(false);
  const [editMode, setEditMode] = useState<'create' | 'edit'>('create');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editLoading, setEditLoading] = useState(false);
  const [editForm] = Form.useForm();

  const [resetVisible, setResetVisible] = useState(false);
  const [resetId, setResetId] = useState<string | null>(null);
  const [resetLoading, setResetLoading] = useState(false);
  const [resetForm] = Form.useForm();

  // 角色选项
  const [roleOptions, setRoleOptions] = useState<RoleOption[]>([]);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getUserList(pagination.current, pagination.pageSize, keyword || undefined);
      setData(res.items);
      setPagination((prev) => ({ ...prev, total: res.total }));
    } catch {
      // request 拦截器已处理错误提示
    } finally {
      setLoading(false);
    }
  }, [pagination.current, pagination.pageSize, keyword]);

  useEffect(() => {
    getRoleList().then(setRoleOptions).catch(() => {});
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  function openCreate() {
    setEditMode('create');
    setEditingId(null);
    editForm.resetFields();
    editForm.setFieldsValue({ isActive: true, roleIds: [] });
    setEditVisible(true);
  }

  async function openEdit(record: UserListItem) {
    setEditMode('edit');
    setEditingId(record.id);
    editForm.resetFields();
    try {
      const detail = await getUserById(record.id);
      editForm.setFieldsValue({
        username: detail.username,
        displayName: detail.displayName,
        email: detail.email,
        isActive: detail.isActive,
        roleIds: detail.roleIds,
      });
    } catch {
      // ignore
    }
    setEditVisible(true);
  }

  async function handleEditOk() {
    try {
      const values = await editForm.validate();
      setEditLoading(true);
      if (editMode === 'create') {
        await createUser(values);
        Message.success(t['user.add.success']);
      } else {
        await updateUser(editingId!, {
          displayName: values.displayName,
          email: values.email,
          isActive: values.isActive,
          roleIds: values.roleIds || [],
        });
        Message.success(t['user.edit.success']);
      }
      setEditVisible(false);
      fetchData();
    } catch (err) {
      // 校验失败或 API 错误
    } finally {
      setEditLoading(false);
    }
  }

  function openReset(record: UserListItem) {
    setResetId(record.id);
    resetForm.resetFields();
    setResetVisible(true);
  }

  async function handleResetOk() {
    try {
      const values = await resetForm.validate();
      setResetLoading(true);
      await resetPassword(resetId!, values.newPassword);
      Message.success(t['user.reset.success']);
      setResetVisible(false);
    } catch {
      // ignore
    } finally {
      setResetLoading(false);
    }
  }

  async function handleToggleStatus(record: UserListItem) {
    try {
      await updateUserStatus(record.id, !record.isActive);
      Message.success(t['user.status.success']);
      fetchData();
    } catch {
      // ignore
    }
  }

  const columns = [
    { title: t['user.username'], dataIndex: 'username', width: 120 },
    { title: t['user.displayName'], dataIndex: 'displayName', width: 120 },
    { title: t['user.email'], dataIndex: 'email', width: 180 },
    {
      title: t['user.roles'],
      dataIndex: 'roleNames',
      render: (roleNames: string[]) =>
        roleNames?.map((n) => <Tag key={n} color="arcoblue">{n}</Tag>) || '-',
    },
    {
      title: t['user.status'],
      dataIndex: 'isActive',
      width: 80,
      render: (isActive: boolean) =>
        isActive ? <Tag color="green">{t['user.active']}</Tag> : <Tag>{t['user.inactive']}</Tag>,
    },
    {
      title: t['user.actions'],
      dataIndex: 'operations',
      width: 220,
      render: (_: unknown, record: UserListItem) => (
        <Space>
          <Button type="text" size="small" onClick={() => openEdit(record)}>
            {t['user.edit']}
          </Button>
          <Button type="text" size="small" onClick={() => openReset(record)}>
            {t['user.resetPassword']}
          </Button>
          <Popconfirm
            title={record.isActive ? t['user.disable'] : t['user.enable']}
            onOk={() => handleToggleStatus(record)}
          >
            <Button type="text" size="small" status={record.isActive ? 'warning' : 'success'}>
              {record.isActive ? t['user.disable'] : t['user.enable']}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <Space style={{ marginBottom: 16, width: '100%', justifyContent: 'space-between' }}>
        <Input.Search
          placeholder={t['user.search.placeholder']}
          onSearch={(v) => { setKeyword(v); setPagination((p) => ({ ...p, current: 1 })); }}
          style={{ width: 300 }}
          prefix={<IconSearch />}
          allowClear
        />
        <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
          {t['user.add']}
        </Button>
      </Space>

      <Table
        rowKey="id"
        columns={columns}
        data={data}
        loading={loading}
        pagination={{
          ...pagination,
          showTotal: true,
          sizeCanChange: true,
          onChange: (current, pageSize) => setPagination((p) => ({ ...p, current, pageSize })),
        }}
      />

      {/* 新增/编辑抽屉 */}
      <Drawer
        title={editMode === 'create' ? t['user.add'] : t['user.edit']}
        visible={editVisible}
        onOk={handleEditOk}
        onCancel={() => setEditVisible(false)}
        confirmLoading={editLoading}
        width={480}
        unmountOnExit
      >
        <Form form={editForm} layout="vertical">
          <FormItem
            label={t['user.username']}
            field="username"
            rules={[{ required: true }]}
          >
            <Input placeholder={t['user.username']} disabled={editMode === 'edit'} />
          </FormItem>
          <FormItem
            label={t['user.displayName']}
            field="displayName"
            rules={[{ required: true }]}
          >
            <Input placeholder={t['user.displayName']} />
          </FormItem>
          <FormItem label={t['user.email']} field="email">
            <Input placeholder={t['user.email']} />
          </FormItem>
          {editMode === 'create' && (
            <FormItem
              label={t['user.password']}
              field="password"
              rules={[{ required: true }]}
            >
              <Input.Password placeholder={t['user.password']} />
            </FormItem>
          )}
          <FormItem label={t['user.assignRoles']} field="roleIds">
            <Select
              placeholder={t['user.assignRoles']}
              multiple
              allowClear
            >
              {roleOptions.map((r) => (
                <Select.Option key={r.id} value={r.id}>
                  {r.name}
                </Select.Option>
              ))}
            </Select>
          </FormItem>
          {editMode === 'edit' && (
            <FormItem
              label={t['user.status']}
              field="isActive"
              triggerPropName="checked"
              rules={[{ type: 'boolean' }]}
            >
              <Switch />
            </FormItem>
          )}
        </Form>
      </Drawer>

      {/* 重置密码抽屉 */}
      <Drawer
        title={t['user.resetPassword']}
        visible={resetVisible}
        onOk={handleResetOk}
        onCancel={() => setResetVisible(false)}
        confirmLoading={resetLoading}
        width={400}
        unmountOnExit
      >
        <Form form={resetForm} layout="vertical">
          <FormItem
            label={t['user.newPassword']}
            field="newPassword"
            rules={[{ required: true }]}
          >
            <Input.Password placeholder={t['user.newPassword']} />
          </FormItem>
          <FormItem
            label={t['user.confirmPassword']}
            field="confirmPassword"
            rules={[
              { required: true },
              {
                validator: (value, cb) => {
                  if (value !== resetForm.getFieldValue('newPassword')) {
                    cb(t['user.password.mismatch']);
                  } else {
                    cb();
                  }
                },
              },
            ]}
          >
            <Input.Password placeholder={t['user.confirmPassword']} />
          </FormItem>
        </Form>
      </Drawer>
    </div>
  );
}
```

- [ ] **Step 4: 验证 TS 编译**

Run: `cd frontend && npx tsc --noEmit 2>&1 | head -20`
Expected: 无报错（仅新增文件的错误需要关注）

- [ ] **Step 5: Commit**

```bash
git add frontend/src/api/user.ts frontend/src/api/role.ts frontend/src/api/permission.ts frontend/src/pages/system/user/
git commit -m "feat(fe): 用户管理页 (表格 + 新增/编辑/重置密码抽屉) + API 模块"
```

---

## Task 9: 前端 — 角色管理页

**Files:**
- Create: `frontend/src/pages/system/role/index.tsx`
- Create: `frontend/src/pages/system/role/locale/index.ts`

**Interfaces:**
- Consumes: `src/api/role.ts`、`src/api/permission.ts`（Task 8）
- Produces: 角色管理页（表格 + Drawer 抽屉 + 权限树勾选）

- [ ] **Step 1: 创建角色管理页 i18n**

`frontend/src/pages/system/role/locale/index.ts`：
```typescript
export default {
  'en-US': {
    'role.title': 'Role Management',
    'role.add': 'Add Role',
    'role.edit': 'Edit Role',
    'role.name': 'Role Name',
    'role.code': 'Code',
    'role.description': 'Description',
    'role.userCount': 'Users',
    'role.permissionCount': 'Permissions',
    'role.actions': 'Actions',
    'role.assignPermissions': 'Assign Permissions',
    'role.delete': 'Delete',
    'role.delete.confirm': 'Are you sure to delete this role?',
    'role.add.success': 'Role created successfully',
    'role.edit.success': 'Role updated successfully',
    'role.delete.success': 'Role deleted successfully',
  },
  'zh-CN': {
    'role.title': '角色管理',
    'role.add': '新增角色',
    'role.edit': '编辑角色',
    'role.name': '角色名',
    'role.code': '编码',
    'role.description': '描述',
    'role.userCount': '用户数',
    'role.permissionCount': '权限数',
    'role.actions': '操作',
    'role.assignPermissions': '分配权限',
    'role.delete': '删除',
    'role.delete.confirm': '确定删除该角色吗？',
    'role.add.success': '角色创建成功',
    'role.edit.success': '角色更新成功',
    'role.delete.success': '角色删除成功',
  },
};
```

- [ ] **Step 2: 创建角色管理页**

`frontend/src/pages/system/role/index.tsx`：

```tsx
import React, { useEffect, useState, useCallback } from 'react';
import {
  Table,
  Button,
  Drawer,
  Form,
  Input,
  Tree,
  Tag,
  Popconfirm,
  Message,
  Space,
} from '@arco-design/web-react';
import { IconPlus } from '@arco-design/web-react/icon';
import useLocale from '@/utils/useLocale';
import {
  getRoleList,
  getRoleById,
  createRole,
  updateRole,
  deleteRole,
  RoleListItem,
} from '@/api/role';
import { getPermissionList, PermissionItem } from '@/api/permission';
import locale from './locale';

const FormItem = Form.Item;
const TreeNode = Tree.Node;

// 将权限列表按模块前缀（code 的第一段）分组为树结构
function buildPermissionTree(permissions: PermissionItem[]) {
  const groups: Record<string, PermissionItem[]> = {};
  permissions.forEach((p) => {
    const module = p.code.split(':')[0];
    if (!groups[module]) groups[module] = [];
    groups[module].push(p);
  });
  return Object.entries(groups).map(([module, perms]) => ({
    key: `group-${module}`,
    title: module,
    children: perms.map((p) => ({ key: p.id, title: p.name })),
  }));
}

export default function RoleManagement() {
  const t = useLocale(locale);
  const [data, setData] = useState<RoleListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [permissions, setPermissions] = useState<PermissionItem[]>([]);
  const [treeData, setTreeData] = useState<any[]>([]);

  const [drawerVisible, setDrawerVisible] = useState(false);
  const [editMode, setEditMode] = useState<'create' | 'edit'>('create');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [drawerLoading, setDrawerLoading] = useState(false);
  const [form] = Form.useForm();
  const [checkedKeys, setCheckedKeys] = useState<string[]>([]);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getRoleList();
      setData(res);
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
    getPermissionList().then((perms) => {
      setPermissions(perms);
      setTreeData(buildPermissionTree(perms));
    });
  }, [fetchData]);

  function openCreate() {
    setEditMode('create');
    setEditingId(null);
    form.resetFields();
    setCheckedKeys([]);
    setDrawerVisible(true);
  }

  async function openEdit(record: RoleListItem) {
    setEditMode('edit');
    setEditingId(record.id);
    form.resetFields();
    try {
      const detail = await getRoleById(record.id);
      form.setFieldsValue({
        name: detail.name,
        code: detail.code,
        description: detail.description,
      });
      setCheckedKeys(detail.permissionIds);
    } catch {
      // ignore
    }
    setDrawerVisible(true);
  }

  async function handleOk() {
    try {
      const values = await form.validate();
      setDrawerLoading(true);
      if (editMode === 'create') {
        await createRole(values);
        Message.success(t['role.add.success']);
      } else {
        await updateRole(editingId!, {
          name: values.name,
          description: values.description,
          permissionIds: checkedKeys.filter((k) => !k.startsWith('group-')),
        });
        Message.success(t['role.edit.success']);
      }
      setDrawerVisible(false);
      fetchData();
    } catch {
      // ignore
    } finally {
      setDrawerLoading(false);
    }
  }

  async function handleDelete(id: string) {
    try {
      await deleteRole(id);
      Message.success(t['role.delete.success']);
      fetchData();
    } catch {
      // ignore
    }
  }

  const columns = [
    { title: t['role.name'], dataIndex: 'name', width: 120 },
    { title: t['role.code'], dataIndex: 'code', width: 120 },
    { title: t['role.description'], dataIndex: 'description' },
    { title: t['role.userCount'], dataIndex: 'userCount', width: 80 },
    { title: t['role.permissionCount'], dataIndex: 'permissionCount', width: 90 },
    {
      title: t['role.actions'],
      dataIndex: 'operations',
      width: 140,
      render: (_: unknown, record: RoleListItem) => (
        <Space>
          <Button type="text" size="small" onClick={() => openEdit(record)}>
            {t['role.edit']}
          </Button>
          <Popconfirm
            title={t['role.delete.confirm']}
            onOk={() => handleDelete(record.id)}
            disabled={record.code === 'admin'}
          >
            <Button type="text" size="small" status="danger" disabled={record.code === 'admin'}>
              {t['role.delete']}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <Button
        type="primary"
        icon={<IconPlus />}
        style={{ marginBottom: 16 }}
        onClick={openCreate}
      >
        {t['role.add']}
      </Button>

      <Table rowKey="id" columns={columns} data={data} loading={loading} pagination={false} />

      <Drawer
        title={editMode === 'create' ? t['role.add'] : t['role.edit']}
        visible={drawerVisible}
        onOk={handleOk}
        onCancel={() => setDrawerVisible(false)}
        confirmLoading={drawerLoading}
        width={480}
        unmountOnExit
      >
        <Form form={form} layout="vertical">
          <FormItem label={t['role.name']} field="name" rules={[{ required: true }]}>
            <Input placeholder={t['role.name']} />
          </FormItem>
          <FormItem label={t['role.code']} field="code" rules={[{ required: true }]}>
            <Input placeholder={t['role.code']} disabled={editMode === 'edit'} />
          </FormItem>
          <FormItem label={t['role.description']} field="description">
            <Input.TextArea placeholder={t['role.description']} />
          </FormItem>
          {editMode === 'edit' && (
            <FormItem label={t['role.assignPermissions']}>
              <Tree
                checkable
                checkedKeys={checkedKeys}
                onCheck={(keys) => setCheckedKeys(keys as string[])}
                treeData={treeData}
              />
            </FormItem>
          )}
        </Form>
      </Drawer>
    </div>
  );
}
```

> 注意：权限分配 Tree 只在编辑模式显示（新增角色先创建，再编辑分配权限，避免新增时权限树交互复杂化）。

- [ ] **Step 3: 验证 TS 编译**

Run: `cd frontend && npx tsc --noEmit 2>&1 | head -20`
Expected: 无报错

- [ ] **Step 4: Commit**

```bash
git add frontend/src/pages/system/role/
git commit -m "feat(fe): 角色管理页 (表格 + 抽屉 + 权限树分配)"
```

---

## Task 10: 前端 — 权限列表页与端到端验证

**Files:**
- Create: `frontend/src/pages/system/permission/index.tsx`
- Create: `frontend/src/pages/system/permission/locale/index.ts`

**Interfaces:**
- Consumes: `src/api/permission.ts`（Task 8）
- Produces: 权限只读列表页 + 前端端到端验证完成

- [ ] **Step 1: 创建权限列表页 i18n**

`frontend/src/pages/system/permission/locale/index.ts`：
```typescript
export default {
  'en-US': {
    'permission.title': 'Permissions',
    'permission.code': 'Code',
    'permission.name': 'Name',
    'permission.description': 'Description',
  },
  'zh-CN': {
    'permission.title': '权限列表',
    'permission.code': '权限编码',
    'permission.name': '名称',
    'permission.description': '描述',
  },
};
```

- [ ] **Step 2: 创建权限列表页**

`frontend/src/pages/system/permission/index.tsx`：
```tsx
import React, { useEffect, useState } from 'react';
import { Table } from '@arco-design/web-react';
import useLocale from '@/utils/useLocale';
import { getPermissionList, PermissionItem } from '@/api/permission';
import locale from './locale';

export default function PermissionList() {
  const t = useLocale(locale);
  const [data, setData] = useState<PermissionItem[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    getPermissionList()
      .then(setData)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const columns = [
    { title: t['permission.code'], dataIndex: 'code', width: 200 },
    { title: t['permission.name'], dataIndex: 'name', width: 150 },
    { title: t['permission.description'], dataIndex: 'description' },
  ];

  return (
    <Table
      rowKey="id"
      columns={columns}
      data={data}
      loading={loading}
      pagination={false}
    />
  );
}
```

- [ ] **Step 3: 验证 TS 编译**

Run: `cd frontend && npx tsc --noEmit 2>&1 | head -20`
Expected: 无报错

- [ ] **Step 4: 启动前后端做端到端验证**

后端（终端 1）：
```bash
cd backend/src/OneCup.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls "http://localhost:5233"
```

前端（终端 2）：
```bash
cd frontend && npm run dev
```

验证项：
1. 用 admin / Admin@123 登录，左侧菜单只有「系统管理」（无 demo 菜单）
2. 点「用户管理」→ 看到 admin 用户列表，能搜索、分页
3. 点「新增用户」→ 抽屉滑出，填表单保存成功
4. 点「编辑」→ 抽屉滑出，用户名只读
5. 点「重置密码」→ 抽屉滑出，输入新密码保存
6. 点「角色管理」→ 看到 admin + developer 角色
7. 点 developer「编辑」→ 权限树勾选，保存
8. admin 角色的删除按钮 disabled
9. 点「权限列表」→ 看到 13 个权限（只读）
10. 尝试禁用 admin 用户 → 后端拒绝（400）

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/system/permission/
git commit -m "feat(fe): 权限列表页 (只读) + 系统管理三件套完成"
```

---

## Self-Review 记录

完成全部任务编写后的自查：

1. **Spec 覆盖：**
   - 用户管理 CRUD（列表/新增/编辑/重置密码/禁用启用）→ Task 2 + Task 5 + Task 8 ✅
   - 角色管理 CRUD（列表/新增/编辑/分配权限/删除）→ Task 3 + Task 5 + Task 9 ✅
   - 权限列表（只读）→ Task 3 + Task 5 + Task 10 ✅
   - 菜单重构（删 demo + 新增系统管理）→ Task 7 ✅
   - admin 保护（不可禁用/不可移除角色）→ Task 2 ✅
   - admin 角色不可删除 → Task 3 ✅
   - 角色删除前校验 → Task 3 ✅
   - JWT Policy 授权 + admin 通配 → Task 5 ✅
   - 验证标准 8 条 → Task 6 + Task 10 ✅

2. **占位符：** 无 TBD/TODO ✅

3. **类型一致性：**
   - DTO 属性名在后端 DTO、前端 interface 中一致（UserListItem.username/displayName/isActive/roleNames 等）✅
   - API 路径前后端一致（/api/users, /api/roles, /api/permissions）✅
   - IUserService/IRoleService/IPermissionService 方法签名与 Controller 调用一致 ✅

4. **潜在问题（实现时注意）：**
   - `routes.ts` 的 `requiredPermissions` 用 `{ resource: 'system:user', actions: ['manage'] }`，需确认与后端 `perm_codes` 的 `system:user:manage` 能正确匹配。前端 `authentication.ts` 的 `auth` 函数会查 `userPermission[resource]`，而后端 `/api/auth/me` 返回的 permissions 经 `transformPermissions` 转换后，`system:user:manage` → `{ 'system:user': ['manage'] }`，与 resource='system:user' + actions=['manage'] 匹配。admin 的 `*` 通配在前端 `authentication.ts` 已处理。✅
   - Program.cs 的 `AddAuthorization` 被调用两次（认证 Task 一次无 options，这里一次带 options）。后者会追加策略配置，合法但可合并。实现时合并为一次调用更干净。
