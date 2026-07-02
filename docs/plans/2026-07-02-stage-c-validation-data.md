# 阶段 C:输入校验 + 数据修正 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 引入 FluentValidation 输入校验(8 个请求 DTO),实现用户软删除(IsDeleted + 全局查询过滤 + DELETE 端点),补全 product:write 权限定义。

**Architecture:** 在阶段 B 建立的 Clean Architecture 上增量:Validator 放 Application 层(与 DTO 同程序集,程序集扫描注册);软删除用 `ISoftDeletable` 接口选择性实现(只在 User 上,不波及 BaseEntity 的其他子类);权限补充走 SeedData HasData + 新 migration。

**Tech Stack:** .NET 10 / FluentValidation / EF Core / xUnit。

## Global Constraints

- net10.0;Nullable + ImplicitUsings on。
- Application 层零 EF Core 依赖(阶段 B 成果,不得破坏)。
- FluentValidation 包加到 Application.csproj;Validator 放 `OneCup.Application/Validators/`;用 `AddFluentValidationAutoValidation` + `RegisterValidatorsFromAssemblyContaining` 自动拦截注册。
- 密码强度规则:**中等** —— 长度≥8 + 含字母+数字(至少各一个)。CreateUserRequest.Password 和 ResetPasswordRequest.NewPassword 共用,抽共享规则。
- 软删除:**只在 User 实体**。用 `ISoftDeletable` 接口(`bool IsDeleted`)选择性实现,不污染 BaseEntity。username 保持全局唯一(不改索引为过滤索引)。
- `HasQueryFilter(u => !u.IsDeleted)` 全局过滤器:所有常规查询自动排除已删除用户,登录/列表/详情无需逐处改。
- product:write:**只补 SeedData 定义 + HasData,不绑定 developer**(保持 developer 现有能力)。
- 软删除 + product:write 合并为**一个新 migration**。
- `IUserService` 接口签名只**新增** `DeleteAsync`,不改现有方法签名。
- admin 保护:DeleteAsync 复用现有保护模式(`user.Id == SystemConstants.AdminUserId` → 抛 `DomainException("不能删除系统管理员账号")`)。
- 软删除时同步吊销该用户所有未吊销 refresh token。
- 测试:xUnit,集成测试风格(Repository + InMemory),手写 fake。

---

## File Structure

| 文件 | 责任 | 动作 |
|------|------|------|
| `OneCup.Application/Validators/PasswordRules.cs` | 密码强度共享规则(扩展方法) | Create |
| `OneCup.Application/Validators/Auth/` | LoginRequest/RefreshRequest Validator | Create |
| `OneCup.Application/Validators/System/` | User/Role 系列 Validator | Create |
| `OneCup.Application/OneCup.Application.csproj` | 加 FluentValidation 包 | Modify |
| `OneCup.Domain/Entities/ISoftDeletable.cs` | 软删除标记接口 | Create |
| `OneCup.Domain/Entities/User.cs` | 实现 ISoftDeletable + IsDeleted 字段 | Modify |
| `OneCup.Infrastructure/Persistence/Configurations/UserConfiguration.cs` | is_deleted 列映射 + HasQueryFilter | Modify |
| `OneCup.Infrastructure/Persistence/SeedData.cs` | PermProductWrite 常量 | Modify |
| `OneCup.Infrastructure/Persistence/OneCupDbContext.cs` | Seed 加 product:write HasData | Modify |
| `OneCup.Infrastructure/Migrations/` | 新 migration(soft delete + product:write) | Create(via dotnet ef) |
| `OneCup.Application/Interfaces/IUserService.cs` | 加 DeleteAsync | Modify |
| `OneCup.Application/Services/UserService.cs` | 实现 DeleteAsync(admin 保护 + 吊销 token) | Modify |
| `OneCup.Api/Controllers/UsersController.cs` | DELETE 端点 | Modify |
| `OneCup.Api/Program.cs` | 注册 FluentValidation | Modify |
| 测试文件 | Validator 测试 + Delete 测试 | Create/Modify |

---

## Task 1: FluentValidation 接入 + 密码强度共享规则

**Files:**
- Modify: `backend/src/OneCup.Application/OneCup.Application.csproj`(加包)
- Create: `backend/src/OneCup.Application/Validators/PasswordRules.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`(注册)
- Test: `backend/tests/OneCup.UnitTests/Validators/PasswordRulesTests.cs`

**Interfaces:**
- Produces: `PasswordRules.BeMediumStrength(string)`(静态方法,校验长度≥8 且含字母且含数字)。注册:`AddFluentValidationAutoValidation(c => c.RegisterValidatorsFromAssemblyContaining<PasswordRules>())`。

- [ ] **Step 1: 加 FluentValidation 包到 Application.csproj**

在 `backend/src/OneCup.Application/OneCup.Application.csproj` 的 PackageReference ItemGroup 加:
```xml
    <PackageReference Include="FluentValidation" Version="11.10.0" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.10.0" />
```
> 注:`FluentValidation.DependencyInjectionExtensions` 提供 `RegisterValidatorsFromAssemblyContaining`。自动拦截(`AddFluentValidationAutoValidation`)在 `FluentValidation.AspNetCore`——但该包到 .NET 10 可能版本对齐问题。**采用更现代的方式**:`FluentValidation` 核心 + 手写一个轻量的 action filter 或在 Controller 前 Service 层手动校验。
>
> **决策修正**:为避免 AspNetCore 集成包的版本/兼容风险,且阶段 B 强调"配置在 Program.cs、规则在 Application 层",采用**手动校验**模式:Validator 注册到 DI,在 Service 层入口(或一个共享校验扩展)手动调 `ValidateAsync` 并抛 `DomainException`。这样 Application 层不依赖 AspNetCore,校验逻辑可单测,且与现有 DomainException→400 异常映射统一。
>
> 因此只需 `FluentValidation` 核心 + `FluentValidation.DependencyInjectionExtensions`。在 Program.cs 注册:`builder.Services.AddValidatorsFromAssemblyContaining<PasswordRules>();`(该扩展方法来自 DependencyInjectionExtensions 包)。

Run: `cd backend/src/OneCup.Application && dotnet restore`

- [ ] **Step 2: 写密码强度测试**

```csharp
// backend/tests/OneCup.UnitTests/Validators/PasswordRulesTests.cs
using OneCup.Application.Validators;

namespace OneCup.UnitTests.Validators;

public class PasswordRulesTests
{
    [Theory]
    [InlineData("Admin@123", true)]      // 字母+数字+符号(中等要求字母+数字即可,符号额外)
    [InlineData("password1", true)]       // 字母+数字,刚好满足
    [InlineData("12345678", false)]       // 纯数字,无字母
    [InlineData("abcdefgh", false)]       // 纯字母,无数字
    [InlineData("Ab1", false)]            // 太短
    [InlineData("", false)]
    public void BeMediumStrength_validates(string pwd, bool expected)
    {
        Assert.Equal(expected, PasswordRules.BeMediumStrength(pwd));
    }
}
```

- [ ] **Step 3: 运行验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~PasswordRulesTests"`
Expected: FAIL — PasswordRules 不存在。

- [ ] **Step 4: 实现 PasswordRules**

```csharp
// backend/src/OneCup.Application/Validators/PasswordRules.cs
namespace OneCup.Application.Validators;

/// <summary>密码强度共享规则。中等强度:长度≥8 且含字母且含数字。</summary>
public static class PasswordRules
{
    public static bool BeMediumStrength(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return false;
        var hasLetter = password.Any(char.IsLetter);
        var hasDigit = password.Any(char.IsDigit);
        return hasLetter && hasDigit;
    }
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~PasswordRulesTests"`
Expected: PASS(6 用例)。

- [ ] **Step 6: 在 Program.cs 注册 Validator**

在 `Program.cs` Service 注册区(AddControllers 之后)加:
```csharp
builder.Services.AddValidatorsFromAssemblyContaining<PasswordRules>();
```
顶部 using 加 `using FluentValidation;`。

- [ ] **Step 7: 构建验证**

Run: `dotnet build backend/src/OneCup.Api/OneCup.Api.csproj`
Expected: 成功。

- [ ] **Step 8: 提交**

```bash
git add backend/src/OneCup.Application/OneCup.Application.csproj backend/src/OneCup.Application/Validators/PasswordRules.cs backend/src/OneCup.Api/Program.cs backend/tests/OneCup.UnitTests/Validators/
git commit -m "feat(val): FluentValidation 接入 + 密码强度中等规则"
```

---

## Task 2: 8 个请求 DTO 的 Validator

**Files:**
- Create: `backend/src/OneCup.Application/Validators/Auth/LoginRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/Auth/RefreshRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/CreateUserRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/UpdateUserRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/ResetPasswordRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/CreateRoleRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/UpdateRoleRequestValidator.cs`
- Test: `backend/tests/OneCup.UnitTests/Validators/`(各 Validator 测试)

> UpdateStatusRequest 只有一个 bool IsActive,无格式校验意义,不加 Validator(无字段需校验)。

**Interfaces:**
- Consumes: `PasswordRules.BeMediumStrength`。
- Produces: 7 个 `AbstractValidator<T>`(每个 DTO 一个),规则见下表。

| Validator | 字段 | 规则 |
|-----------|------|------|
| LoginRequestValidator | Username | NotEmpty, MaximumLength(50) |
| | Password | NotEmpty |
| RefreshRequestValidator | RefreshToken | NotEmpty |
| CreateUserRequestValidator | Username | NotEmpty, Length(3,50) |
| | DisplayName | NotEmpty, Length(1,50) |
| | Email | EmailAddress(可空时跳过), MaximumLength(100) |
| | Password | NotEmpty, Must(PasswordRules.BeMediumStrength).WithMessage("密码至少8位且含字母和数字") |
| | RoleIds | NotEmpty(至少分配一个角色) |
| UpdateUserRequestValidator | DisplayName | NotEmpty, Length(1,50) |
| | Email | EmailAddress(可空时跳过), MaximumLength(100) |
| | RoleIds | NotEmpty |
| ResetPasswordRequestValidator | NewPassword | NotEmpty, Must(PasswordRules.BeMediumStrength) |
| CreateRoleRequestValidator | Name | NotEmpty, MaximumLength(50) |
| | Code | NotEmpty, Matches("^[a-z][a-z0-9_:]*$").WithMessage("角色编码只能含小写字母/数字/下划线/冒号"), MaximumLength(50) |
| UpdateRoleRequestValidator | Name | NotEmpty, MaximumLength(50) |
| | PermissionIds | (可空,允许清空权限)无强制规则 |

- [ ] **Step 1: 写 Validator 测试(先写关键几个)**

```csharp
// backend/tests/OneCup.UnitTests/Validators/CreateUserRequestValidatorTests.cs
using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.Validators;

public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator _validator = new();

    private static CreateUserRequest Valid() => new()
    {
        Username = "alice", DisplayName = "Alice", Password = "Password1", RoleIds = [Guid.NewGuid()]
    };

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.Validate(Valid());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Empty_username_fails()
    {
        var req = Valid(); req.Username = "";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Short_username_under_3_fails()
    {
        var req = Valid(); req.Username = "ab";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Weak_password_no_digit_fails()
    {
        var req = Valid(); req.Password = "password";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Empty_roleIds_fails()
    {
        var req = Valid(); req.RoleIds = [];
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Invalid_email_fails()
    {
        var req = Valid(); req.Email = "not-an-email";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Valid_email_passes()
    {
        var req = Valid(); req.Email = "alice@example.com";
        Assert.True(_validator.Validate(req).IsValid);
    }
}
```

```csharp
// backend/tests/OneCup.UnitTests/Validators/CreateRoleRequestValidatorTests.cs
using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.Validators;

public class CreateRoleRequestValidatorTests
{
    private readonly CreateRoleRequestValidator _validator = new();

    [Fact]
    public void Valid_code_passes() => Assert.True(_validator.Validate(new CreateRoleRequest { Name = "测试", Code = "test_role" }).IsValid);

    [Theory]
    [InlineData("Admin")]      // 大写
    [InlineData("test role")]  // 空格
    [InlineData("")]           // 空
    public void Invalid_code_fails(string code) =>
        Assert.False(_validator.Validate(new CreateRoleRequest { Name = "测试", Code = code }).IsValid);
}
```
(对 LoginRequest/ResetPassword/UpdateUser 各写 2-3 个关键用例;RefreshRequest 写 1 个 NotEmpty。)

- [ ] **Step 2: 运行验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~Validators"`
Expected: FAIL — Validator 类不存在。

- [ ] **Step 3: 实现 7 个 Validator**

按上表规则实现。示例(CreateUserRequestValidator):
```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().Length(3, 50);
        RuleFor(x => x.DisplayName).NotEmpty().Length(1, 50);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Password).NotEmpty().Must(PasswordRules.BeMediumStrength)
            .WithMessage("密码至少8位且含字母和数字");
        RuleFor(x => x.RoleIds).NotEmpty();
    }
}
```
CreateRoleRequest 的 Code 用 `.Matches("^[a-z][a-z0-9_:]*$")`。

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~Validators"`
Expected: PASS。

- [ ] **Step 5: 在 Service 层手动调用校验**

在需要校验的 Service 方法入口手动调 Validator(因采用手动校验模式,非自动拦截)。最干净的做法:在 `Program.cs` 或一个共享扩展里,但更简单的是**在 Controller 里注入 IValidator 并校验**——但那样 Controller 变胖。

**采用方案**:在每个 Service 的 Create/Update/Reset 等接受请求的方法里,注入对应 `IValidator<T>`,在方法开头:
```csharp
var validationResult = await _createUserValidator.ValidateAsync(request, ct);
if (!validationResult.IsValid)
{
    throw new DomainException(string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)));
}
```
> DomainException 已被全局异常处理器映射为 400。这样校验在 Application 层,可单测,统一走 DomainException→400。
> 需要在 AuthService(UserService/RoleService/AuthService)构造函数注入对应 Validator。

> 注:此步改动 UserService/RoleService/AuthService 构造函数(加 Validator 依赖)+ 对应测试构造。工作量适中但清晰。

- [ ] **Step 6: 全量测试**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: 全 PASS(含新 Validator 测试 + 现有用例;注意构造函数变化要同步改测试)。

- [ ] **Step 7: 提交**

```bash
git add -A
git commit -m "feat(val): 8个请求 DTO FluentValidation 校验 (Service 层手动调用)"
```

---

## Task 3: 软删除 — 实体 + 配置 + 全局过滤器

**Files:**
- Create: `backend/src/OneCup.Domain/Entities/ISoftDeletable.cs`
- Modify: `backend/src/OneCup.Domain/Entities/User.cs`
- Modify: `backend/src/OneCup.Infrastructure/Persistence/Configurations/UserConfiguration.cs`

**Interfaces:**
- Produces: `ISoftDeletable`(`bool IsDeleted { get; set; }`)、`User : ISoftDeletable`、`UserConfiguration` 加 `is_deleted` 列 + `HasQueryFilter(u => !u.IsDeleted)`。

- [ ] **Step 1: 定义 ISoftDeletable**

```csharp
// backend/src/OneCup.Domain/Entities/ISoftDeletable.cs
namespace OneCup.Domain.Entities;

/// <summary>软删除标记接口。实现的实体加 IsDeleted 字段 + EF 全局查询过滤器。</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
```

- [ ] **Step 2: User 实现 ISoftDeletable**

在 `User.cs`:`public class User : BaseEntity, ISoftDeletable`,加字段:
```csharp
    public bool IsDeleted { get; set; } = false;
```

- [ ] **Step 3: UserConfiguration 加列 + 全局过滤器**

在 `UserConfiguration.cs` 加:
```csharp
        builder.Property(u => u.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.HasQueryFilter(u => !u.IsDeleted);  // 全局过滤器:常规查询自动排除已删除
```
(username 唯一索引保持不变——决策:全局唯一,不改为过滤索引。)

- [ ] **Step 4: 构建验证**

Run: `dotnet build backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj`
Expected: 成功。

- [ ] **Step 5: 提交**

```bash
git add backend/src/OneCup.Domain/Entities/ISoftDeletable.cs backend/src/OneCup.Domain/Entities/User.cs backend/src/OneCup.Infrastructure/Persistence/Configurations/UserConfiguration.cs
git commit -m "feat(data): 软删除 — ISoftDeletable 接口 + User.IsDeleted + 全局查询过滤器"
```

---

## Task 4: product:write 权限 + 新 migration

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`
- Create: 新 migration(soft delete is_deleted 列 + product:write seed 权限)

- [ ] **Step 1: SeedData 加 PermProductWrite**

在 `SeedData.cs` 加常量(116 = 111 product:read 之后下一可用段):
```csharp
    public static readonly Guid PermProductWrite = Guid.Parse("00000000-0000-0000-0000-000000000116");
```

- [ ] **Step 2: DbContext.Seed 加 product:write HasData**

在 `OneCupDbContext.Seed()` 的权限 HasData 区,product:read 之后加:
```csharp
                new() { Id = SeedData.PermProductWrite, Code = "product:write", Name = "录入/编辑产品", CreatedAt = SeedTimestamp },
```
> 不绑定 developer(决策:保持 developer 现有能力)。

- [ ] **Step 3: 生成新 migration**

Run: `cd backend/src/OneCup.Infrastructure && dotnet ef migrations add AddUserSoftDeleteAndProductWrite --startup-project ../OneCup.Api`
Expected: 生成 migration 文件,含 `is_deleted` 列(users 表)+ product:write 权限 HasData。
> 检查生成的 migration:应含 `AddColumn<bool>("is_deleted", ...)` 和一条 Permission Insert(product:write)。

- [ ] **Step 4: 构建验证**

Run: `dotnet build backend/src/OneCup.Api/OneCup.Api.csproj`
Expected: 成功。

- [ ] **Step 5: 提交**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/SeedData.cs backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs backend/src/OneCup.Infrastructure/Migrations/
git commit -m "feat(data): product:write 权限 + 软删除 migration (AddUserSoftDeleteAndProductWrite)"
```

---

## Task 5: DeleteAsync(软删除实现 + admin 保护 + 吊销 token)

**Files:**
- Modify: `backend/src/OneCup.Application/Interfaces/IUserService.cs`(加 DeleteAsync)
- Modify: `backend/src/OneCup.Application/Services/UserService.cs`(实现)
- Modify: `backend/tests/OneCup.UnitTests/System/UserServiceTests.cs`(加 Delete 测试)

**Interfaces:**
- Produces: `Task IUserService.DeleteAsync(Guid id, CancellationToken ct)`。实现:加载 user(admin 保护)→ IsDeleted=true → 同步吊销 refresh token(需要 `IRepository<RefreshToken>`)→ UoW.SaveChangesAsync。
> 关键:UserService 当前依赖 IRepository<User>/Role/UoW。DeleteAsync 要吊销 refresh token,需**新增** `IRepository<RefreshToken>` 依赖到 UserService。

- [ ] **Step 1: 写失败测试**

在 `UserServiceTests.cs` 加:
```csharp
    [Fact]
    public async Task DeleteAsync_soft_deletes_user()
    {
        var svc = CreateUserService();  // 现有 helper
        await svc.DeleteAsync(DeveloperUserId, default);
        // 全局过滤器使 GetByIdAsync 返回 null(已删除)
        var gone = await svc.GetByIdAsync(DeveloperUserId, default);
        Assert.Null(gone);
    }

    [Fact]
    public async Task DeleteAsync_admin_user_throws()
    {
        var svc = CreateUserService();
        await Assert.ThrowsAsync<DomainException>(() => svc.DeleteAsync(SystemConstants.AdminUserId, default));
    }
```
> CreateUserService helper 需更新:注入 IRepository<RefreshToken>(新依赖)。

- [ ] **Step 2: 运行验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~UserServiceTests"`
Expected: FAIL — DeleteAsync 不存在。

- [ ] **Step 3: 加 DeleteAsync 到接口 + 实现**

`IUserService.cs` 加:
```csharp
    Task DeleteAsync(Guid id, CancellationToken ct = default);
```

`UserService.cs`:
- 构造函数加 `IRepository<RefreshToken> refreshTokens` 依赖 + 字段。
- 实现 DeleteAsync:
```csharp
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct)
            ?? throw new DomainException("用户不存在");
        if (user.Id == SystemConstants.AdminUserId)
            throw new DomainException("不能删除系统管理员账号");

        user.IsDeleted = true;

        // 同步吊销该用户所有未吊销 refresh token
        var activeTokens = await _refreshTokens.ListAsync(new ActiveRefreshTokensByUserSpec(id), ct);
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
        }

        await _uow.SaveChangesAsync(ct);
    }
```
> `ActiveRefreshTokensByUserSpec` 已在 AuthSpecs(Task 8 阶段B)定义,复用。需 using `OneCup.Application.Specifications`。

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~UserServiceTests"`
Expected: PASS(含新 Delete 测试 + 现有用例)。

- [ ] **Step 5: 提交**

```bash
git add backend/src/OneCup.Application/Interfaces/IUserService.cs backend/src/OneCup.Application/Services/UserService.cs backend/tests/OneCup.UnitTests/System/UserServiceTests.cs
git commit -m "feat(data): 用户软删除 DeleteAsync (admin保护 + 同步吊销token)"
```

---

## Task 6: DELETE 端点 + 冒烟验证

**Files:**
- Modify: `backend/src/OneCup.Api/Controllers/UsersController.cs`(加 DELETE 端点)
- 验证:全量构建+测试

- [ ] **Step 1: 加 DELETE 端点**

在 `UsersController.cs` 加:
```csharp
    /// <summary>删除用户(软删除)。</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(Status204NoContent)]
    [ProducesResponseType(Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _userService.DeleteAsync(id, ct);
        return NoContent();
    }
```
> DeleteAsync 在用户不存在时抛 DomainException(→400)而非 404。若要 404 语义,需在 Controller 捕获;但保持与现有 UpdateAsync 等一致的"抛异常→全局处理"风格,这里 DomainException→400 可接受(用户不存在是客户端错误)。或改为先 GetById 判断返回 404。**保持简单**:沿用抛 DomainException,全局映射 400。

- [ ] **Step 2: 全量构建+测试**

Run: `dotnet build backend/src/OneCup.Api/OneCup.Api.csproj && dotnet test backend/tests/OneCup.UnitTests`
Expected: 构建成功,全 PASS(除 8 NumberingServiceConcurrencyTests Docker 噪音)。

- [ ] **Step 3: 验证软删除全局过滤器生效(InMemory)**

> InMemory provider 支持 HasQueryFilter 吗?**EF Core InMemory 不完全支持 HasQueryFilter**——这是已知限制。InMemory 下 HasQueryFilter 被忽略,已删除用户仍可能被查到。因此 UserServiceTests 的"删除后 GetById 返回 null"在 InMemory 下**可能失败**。
>
> **应对**:若 InMemory 不支持 QueryFilter,DeleteAsync 测试改为**直接断言 IsDeleted 标志**(用 DbContext 直接查 `db.Users.IgnoreQueryFilters().First(u => u.Id == id).IsDeleted`),而非依赖 GetById 返回 null。在 Task 5 Step 1 的测试里用此断言方式。
> **在 Task 5 实现时先验证 InMemory 对 QueryFilter 的行为,据此调整测试断言**。

- [ ] **Step 4: 提交**

```bash
git add backend/src/OneCup.Api/Controllers/UsersController.cs
git commit -m "feat(data): 用户 DELETE 端点 (软删除)"
```

---

## Self-Review(写作后自检)

**1. Spec 覆盖(spec 第 5 节 C):**
- 5.1 FluentValidation → Task 1+2 ✓
- 5.2 product:write → Task 4 ✓
- 5.3 软删除 → Task 3+5+6 ✓

**2. InMemory QueryFilter 风险:** Task 6 Step 3 已标注——EF InMemory 可能不执行 HasQueryFilter。Task 5 测试断言方式需据此调整(用 IgnoreQueryFilters 直接查 IsDeleted,不依赖过滤后查询)。**执行 Task 5 时优先验证此行为**。

**3. 类型一致性:** ISoftDeletable 在 Task 3 定义后 User 实现;DeleteAsync 在 Task 5 用 IsDeleted(实体字段)和 ActiveRefreshTokensByUserSpec(阶段B AuthSpecs 已有)。✓

**4. Validator 手动调用模式:** Task 2 Step 5 在 Service 构造函数注入 IValidator,改变 UserService/RoleService/AuthService 构造签名——需同步改所有测试构造。Task 2 已标注。
