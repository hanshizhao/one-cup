# 客户管理模块 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现客户档案 CRUD 模块（后端六边形 + 前端 query-table 列表页 + Modal/Drawer），客户编号创建时事务内由编号系统生成。

**Architecture:** 后端沿用 Repository + Specification + UnitOfWork + FluentValidation 六边形模式；CustomerService.CreateAsync 用 ExecuteInTransactionAsync 包裹"取号+落库"，复用 main 基线已有的权限(107/108)和目标类型(204)种子，不新增任何 Guid、不种子化。前端新建"业务管理"菜单组，复制 query-table 模板。

**Tech Stack:** .NET 10 + EF Core + PostgreSQL（后端）；Vite + React + TypeScript + Arco Design Pro（前端）

## Global Constraints

- **不新增任何 Guid**：权限复用 `PermCustomerRead`(...107)/`PermCustomerWrite`(...108)、目标类型复用 `TargetTypeCustomer`(...204)，全部已在 main 基线（`SeedData.cs`）。
- **不碰 `SeedData.cs` 和 `OneCupDbContext.Seed()`**：客户模块不种子化任何数据；客户编号规则运行时在编号管理配置。
- **EF 迁移命名 `AddCustomerModule`**（contract 3.2，防撞）。
- **后端列名 snake_case**（`HasColumnName`），表名 `customers`（复数）。
- **遵守 `docs/parallel-dev-contract.md`**：只动客户模块 per-file + 末尾追加高冲突文件。
- **工作目录**：`.worktrees/customer-mgmt/`，分支 `feat/customer-mgmt`。
- **前端列表页必须从 `docs/specs/templates/query-table-page.template.tsx` 复制改造**，不从零手写（AGENTS.md 强制）。
- **后端构建命令**：`dotnet build backend/OneCup.sln`；**测试**：`dotnet test backend/OneCup.sln`。
- **前端构建命令**：`cd frontend && npm run build`。

---

## File Structure

**后端新建（per-file 零冲突）：**
- `backend/src/OneCup.Domain/Entities/Customer.cs` — 实体
- `backend/src/OneCup.Application/Dtos/System/CustomerDtos.cs` — DTO
- `backend/src/OneCup.Application/Specifications/CustomerSpecs.cs` — 查询规范
- `backend/src/OneCup.Application/Validators/System/CreateCustomerRequestValidator.cs` — 新建校验
- `backend/src/OneCup.Application/Validators/System/UpdateCustomerRequestValidator.cs` — 编辑校验
- `backend/src/OneCup.Application/Interfaces/ICustomerService.cs` — 服务接口
- `backend/src/OneCup.Application/Services/CustomerService.cs` — 服务实现
- `backend/src/OneCup.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs` — EF 配置
- `backend/src/OneCup.Api/Controllers/CustomersController.cs` — 控制器
- `backend/tests/OneCup.UnitTests/Customer/CustomerServiceTests.cs` — 服务测试
- `backend/tests/OneCup.UnitTests/Customer/CustomerValidatorTests.cs` — 校验测试

**后端修改（高冲突文件，末尾追加）：**
- `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs` — DbSet + ApplyConfiguration
- `backend/src/OneCup.Api/Program.cs` — 授权策略 + DI 注册

**EF 迁移（EF 生成）：**
- `backend/src/OneCup.Infrastructure/Migrations/{ts}_AddCustomerModule.cs` + `.Designer.cs`
- `backend/src/OneCup.Infrastructure/Migrations/OneCupDbContextModelSnapshot.cs`（自动刷新）

**前端新建（per-file 零冲突）：**
- `frontend/src/api/customer.ts` — API 模块
- `frontend/src/pages/business/customer/index.tsx` — 列表页
- `frontend/src/pages/business/customer/form.tsx` — 新建/编辑 Modal
- `frontend/src/pages/business/customer/detail.tsx` — 详情 Drawer
- `frontend/src/pages/business/customer/locale/index.ts` — 页面级 i18n
- `frontend/src/pages/business/customer/style/index.module.less` — 样式

**前端修改（共享文件，追加）：**
- `frontend/src/routes.ts` — 新增 business 菜单组
- `frontend/src/router.tsx` — 新增路由
- `frontend/src/locale/index.ts` — 新增 menu.business 文案

---

## Task 1: Customer 实体 + EF 配置

**Files:**
- Create: `backend/src/OneCup.Domain/Entities/Customer.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`

**Interfaces:**
- Produces: `Customer` 实体类（`BaseEntity, ISoftDeletable`），含字段 Code/Name/ShortName/ContactPerson/ContactPhone/Remark/IsActive/IsDeleted

- [ ] **Step 1: 创建 Customer 实体**

Create `backend/src/OneCup.Domain/Entities/Customer.cs`:

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 客户档案。客户编号由编号系统在创建事务内生成。
/// </summary>
public class Customer : BaseEntity, ISoftDeletable
{
    /// <summary>客户编号（编号系统生成，如 CUST-0001）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>客户名称（全名，唯一）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>客户简称（可重复）</summary>
    public string? ShortName { get; set; }

    /// <summary>联系人</summary>
    public string? ContactPerson { get; set; }

    /// <summary>联系电话</summary>
    public string? ContactPhone { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>启用状态（停用的客户不再用于新业务，但保留历史）</summary>
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; } = false;
}
```

- [ ] **Step 2: 创建 CustomerConfiguration**

Create `backend/src/OneCup.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.ShortName).HasColumnName("short_name").HasMaxLength(50);
        builder.Property(c => c.ContactPerson).HasColumnName("contact_person").HasMaxLength(50);
        builder.Property(c => c.ContactPhone).HasColumnName("contact_phone").HasMaxLength(30);
        builder.Property(c => c.Remark).HasColumnName("remark").HasMaxLength(500);
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasIndex(c => c.Name).IsUnique();

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
```

- [ ] **Step 3: 注册到 DbContext**

Modify `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`.

在已有 `DbSet` 声明区末尾追加（参考现有 `public DbSet<Role> Roles => Set<Role>();` 风格）：

```csharp
// ===== Customer 模块 =====
public DbSet<Customer> Customers => Set<Customer>();
```

在 `OnModelCreating` 方法的 `ApplyConfiguration` 调用区末尾追加：

```csharp
modelBuilder.ApplyConfiguration(new CustomerConfiguration());
```

- [ ] **Step 4: 验证后端编译**

Run: `cd backend && dotnet build OneCup.sln`
Expected: BUILD SUCCEEDED（无错误；Customer 实体和配置已接入）

- [ ] **Step 5: 提交**

```bash
git add backend/src/OneCup.Domain/Entities/Customer.cs \
        backend/src/OneCup.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs \
        backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs
git commit -m "feat(customer): Customer 实体 + EF 配置 + DbSet 注册"
```

---

## Task 2: EF 迁移 AddCustomerModule

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Migrations/{ts}_AddCustomerModule.cs`（EF 生成）
- Modify: `backend/src/OneCup.Infrastructure/Migrations/OneCupDbContextModelSnapshot.cs`（EF 自动刷新）

**Interfaces:**
- Produces: `customers` 表的建表迁移

- [ ] **Step 1: 生成迁移**

Run:
```bash
cd backend
dotnet ef migrations add AddCustomerModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```
Expected: 生成 `{timestamp}_AddCustomerModule.cs` + `.Designer.cs`，刷新 `OneCupDbContextModelSnapshot.cs`。

- [ ] **Step 2: 检查迁移 Up() 内容**

打开生成的 `*_AddCustomerModule.cs`，确认 `Up()` 包含：
- 建表 `customers`（id, code, name, short_name, contact_person, contact_phone, remark, is_active, is_deleted, created_at, updated_at）
- 唯一索引 `IX_customers_code`、`IX_customers_name`

**注意**：不得包含任何 `HasData`（客户模块不种子化）。若误含，检查 DbContext.Seed() 是否被误改，回退后重做。

- [ ] **Step 3: 应用迁移到本地库验证**

Run:
```bash
cd backend
dotnet ef database update --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```
Expected: `Applying migration '{timestamp}_AddCustomerModule'.` + `Done.`，无报错。

若本地无数据库连接（仅构建验证），可跳过此步，但需确认 `dotnet build` 通过。

- [ ] **Step 4: 提交**

```bash
git add backend/src/OneCup.Infrastructure/Migrations/
git commit -m "feat(customer): EF 迁移 AddCustomerModule（建 customers 表）"
```

---

## Task 3: Customer DTO

**Files:**
- Create: `backend/src/OneCup.Application/Dtos/System/CustomerDtos.cs`

**Interfaces:**
- Produces: `CustomerListItemDto`、`CustomerDto`、`CreateCustomerRequest`、`UpdateCustomerRequest`（供 Task 4/5/6 使用）

- [ ] **Step 1: 创建 DTO 文件**

Create `backend/src/OneCup.Application/Dtos/System/CustomerDtos.cs`:

```csharp
namespace OneCup.Application.Dtos.System;

/// <summary>客户列表项（表格行）。</summary>
public class CustomerListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>客户详情（Drawer 只读）。</summary>
public class CustomerDto : CustomerListItemDto
{
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>新建客户请求。Code 不在此处——由系统在事务内生成。</summary>
public class CreateCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>编辑客户请求（字段同 Create，独立类以便 FluentValidation 区分规则）。</summary>
public class UpdateCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
}
```

- [ ] **Step 2: 验证编译**

Run: `cd backend && dotnet build OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: 提交**

```bash
git add backend/src/OneCup.Application/Dtos/System/CustomerDtos.cs
git commit -m "feat(customer): Customer DTO（列表/详情/请求）"
```

---

## Task 4: 查询规范（Specification）

**Files:**
- Create: `backend/src/OneCup.Application/Specifications/CustomerSpecs.cs`

**Interfaces:**
- Consumes: `Specification<T>` 基类（`ApplyCriteria`/`ApplyOrderByDescending`/`ApplyPaging`，覆盖语义，见 `Specification.cs`）
- Produces: `CustomerFilterSpec`、`CustomerPagedSpec`、`CustomerByIdSpec`、`CustomerByNameSpec`

- [ ] **Step 1: 创建规范文件**

Create `backend/src/OneCup.Application/Specifications/CustomerSpecs.cs`:

```csharp
using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>
/// 仅按 keyword/code/isActive 过滤的客户查询规范（不含分页/排序）。
/// 专用于 CountAsync 统计总数——若用带分页的规范统计，
/// Repository.CountAsync 会应用 Skip/Take，导致只统计当前页子集。
/// 与 <see cref="CustomerPagedSpec"/> 共享相同的 Where 条件。
/// </summary>
/// <remarks>
/// 基类 ApplyCriteria 是覆盖语义，多条件必须组合为单一 predicate 后调用一次。
/// </remarks>
public class CustomerFilterSpec : Specification<Customer>
{
    public CustomerFilterSpec(string? keyword, string? code, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(c =>
            (kw == null || c.Name.Contains(kw) || c.ShortName!.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || c.Code.Contains(code)) &&
            (isActive == null || c.IsActive == isActive.Value));
    }
}

/// <summary>客户分页查询（含过滤、按创建时间倒序）。</summary>
public class CustomerPagedSpec : Specification<Customer>
{
    public CustomerPagedSpec(string? keyword, string? code, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(c =>
            (kw == null || c.Name.Contains(kw) || c.ShortName!.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || c.Code.Contains(code)) &&
            (isActive == null || c.IsActive == isActive.Value));

        ApplyOrderByDescending(c => c.CreatedAt);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>按 Id 查询客户（tracked，详情/更新用 FirstOrDefaultAsync）。</summary>
public class CustomerByIdSpec : Specification<Customer>
{
    public CustomerByIdSpec(Guid id)
    {
        ApplyCriteria(c => c.Id == id);
    }
}

/// <summary>名称唯一性校验（配合 AnyIgnoringFiltersAsync，绕过软删除过滤器）。</summary>
public class CustomerByNameSpec : Specification<Customer>
{
    public CustomerByNameSpec(string name, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(c => c.Name == name && (exclude == null || c.Id != exclude.Value));
    }
}
```

- [ ] **Step 2: 验证编译**

Run: `cd backend && dotnet build OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: 提交**

```bash
git add backend/src/OneCup.Application/Specifications/CustomerSpecs.cs
git commit -m "feat(customer): 查询规范（过滤/分页/ById/ByName）"
```

---

## Task 5: FluentValidation 校验器

**Files:**
- Create: `backend/src/OneCup.Application/Validators/System/CreateCustomerRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/UpdateCustomerRequestValidator.cs`
- Test: `backend/tests/OneCup.UnitTests/Customer/CustomerValidatorTests.cs`

**Interfaces:**
- Consumes: `CreateCustomerRequest`/`UpdateCustomerRequest`（Task 3）
- Produces: `CreateCustomerRequestValidator`、`UpdateCustomerRequestValidator`（供 Task 6 服务注入）

- [ ] **Step 1: 写失败的校验测试**

Create `backend/tests/OneCup.UnitTests/Customer/CustomerValidatorTests.cs`:

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.Customer;

public class CustomerValidatorTests
{
    private readonly CreateCustomerRequestValidator _create = new();
    private readonly UpdateCustomerRequestValidator _update = new();

    private static CreateCustomerRequest ValidCreate() => new()
    {
        Name = "深圳市测试服饰有限公司",
        ShortName = "测试服饰",
        ContactPerson = "张三",
        ContactPhone = "0755-12345678",
        Remark = "VIP客户",
        IsActive = true,
    };

    [Fact]
    public void Create_Valid_passes() =>
        Assert.True(_create.Validate(ValidCreate()).IsValid);

    [Fact]
    public void Create_EmptyName_fails() =>
        Assert.False(_create.Validate(ValidCreate() with { Name = "" }).IsValid);

    [Theory]
    [InlineData("深圳市XX服饰有限公司XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX")]
    public void Create_NameOver100_fails(string name) =>
        Assert.False(_create.Validate(ValidCreate() with { Name = name }).IsValid);

    [Fact]
    public void Create_PhoneAcceptsLandline() =>
        Assert.True(_create.Validate(ValidCreate() with { ContactPhone = "0755-12345678-888" }).IsValid);

    [Fact]
    public void Create_PhoneAcceptsMobile() =>
        Assert.True(_create.Validate(ValidCreate() with { ContactPhone = "13800138000" }).IsValid);

    [Fact]
    public void Create_PhoneRejectsLetters() =>
        Assert.False(_create.Validate(ValidCreate() with { ContactPhone = "abcd1234" }).IsValid);

    [Fact]
    public void Update_Valid_passes() =>
        Assert.True(_update.Validate(new UpdateCustomerRequest
        {
            Name = "测试",
            IsActive = false,
        }).IsValid);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `cd backend && dotnet test OneCup.sln --filter "FullyQualifiedName~CustomerValidatorTests"`
Expected: FAIL（编译错误：`CreateCustomerRequestValidator`/`UpdateCustomerRequestValidator` 类型不存在）

- [ ] **Step 3: 创建 CreateCustomerRequestValidator**

Create `backend/src/OneCup.Application/Validators/System/CreateCustomerRequestValidator.cs`:

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>创建客户请求校验。联系电话用宽松正则（允许座机/分机/手机）。</summary>
public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    // 数字、+、-、空格、括号；座机/手机/分机通用
    private static readonly string PhonePattern = @"^[\d+\-()\s]+$";

    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShortName).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.ShortName));
        RuleFor(x => x.ContactPerson).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.ContactPerson));
        RuleFor(x => x.ContactPhone)
            .MaximumLength(30)
            .Matches(PhonePattern)
            .When(x => !string.IsNullOrEmpty(x.ContactPhone));
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
    }
}
```

- [ ] **Step 4: 创建 UpdateCustomerRequestValidator**

Create `backend/src/OneCup.Application/Validators/System/UpdateCustomerRequestValidator.cs`:

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>编辑客户请求校验（字段约束同 Create）。</summary>
public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    private static readonly string PhonePattern = @"^[\d+\-()\s]+$";

    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShortName).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.ShortName));
        RuleFor(x => x.ContactPerson).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.ContactPerson));
        RuleFor(x => x.ContactPhone)
            .MaximumLength(30)
            .Matches(PhonePattern)
            .When(x => !string.IsNullOrEmpty(x.ContactPhone));
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
    }
}
```

- [ ] **Step 5: 运行测试确认通过**

Run: `cd backend && dotnet test OneCup.sln --filter "FullyQualifiedName~CustomerValidatorTests"`
Expected: 7 passed, 0 failed

- [ ] **Step 6: 提交**

```bash
git add backend/src/OneCup.Application/Validators/System/CreateCustomerRequestValidator.cs \
        backend/src/OneCup.Application/Validators/System/UpdateCustomerRequestValidator.cs \
        backend/tests/OneCup.UnitTests/Customer/CustomerValidatorTests.cs
git commit -m "feat(customer): FluentValidation 校验器 + 测试"
```

---

## Task 6: ICustomerService + CustomerService（核心业务逻辑）

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/ICustomerService.cs`
- Create: `backend/src/OneCup.Application/Services/CustomerService.cs`
- Test: `backend/tests/OneCup.UnitTests/Customer/CustomerServiceTests.cs`

**Interfaces:**
- Consumes: `IRepository<Customer>`、`IUnitOfWork`、`INumberingService`、`IValidator<CreateCustomerRequest>`、`IValidator<UpdateCustomerRequest>`、`CustomerFilterSpec`/`CustomerPagedSpec`/`CustomerByIdSpec`/`CustomerByNameSpec`（Task 4）、`NumberTargetTypes.Customer`（main 基线 `Common/NumberTargetTypes.cs`）、`PagedResult<T>`（main 基线 `Common/PagedResult.cs`）
- Produces: `ICustomerService`（供 Task 8 控制器 + DI 注册）

- [ ] **Step 1: 写失败的服务测试**

Create `backend/tests/OneCup.UnitTests/Customer/CustomerServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Services;
using OneCup.Application.Specifications;
using OneCup.Application.Validators.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Customer;

public class CustomerServiceTests
{
    private static (OneCupDbContext db, CustomerService svc, FakeNumberingService numbering) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"customer-test-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);

        var numbering = new FakeNumberingService();
        var svc = new CustomerService(
            new Repository<Customer>(db),
            new UnitOfWork(db),
            numbering,
            new CreateCustomerRequestValidator(),
            new UpdateCustomerRequestValidator());
        return (db, svc, numbering);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateAsync_CreatesCustomer_WithGeneratedCode()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "CUST-0007";

        var result = await svc.CreateAsync(new CreateCustomerRequest
        {
            Name = "深圳XX服饰",
            IsActive = true,
        });

        Assert.Equal("CUST-0007", result.Code);
        Assert.Equal("深圳XX服饰", result.Name);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        var (db, svc, _) = Setup();
        db.Customers.Add(new Customer { Code = "C1", Name = "已存在客户" });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(new CreateCustomerRequest { Name = "已存在客户" }));
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_IgnoresSoftDeleted_Throws()
    {
        // 已软删除客户占用的名称，新建时仍应被识别为占用
        var (db, svc, _) = Setup();
        db.Customers.Add(new Customer { Code = "C1", Name = "软删客户", IsDeleted = true });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(new CreateCustomerRequest { Name = "软删客户" }));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields()
    {
        var (db, svc, _) = Setup();
        var c = new Customer { Code = "C1", Name = "原名", IsActive = true };
        db.Customers.Add(c);
        await db.SaveChangesAsync();

        var result = await svc.UpdateAsync(c.Id, new UpdateCustomerRequest
        {
            Name = "原名",
            ShortName = "新简称",
            IsActive = false,
        });

        Assert.Equal("新简称", result.ShortName);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_KeepOwnName_Allowed()
    {
        var (db, svc, _) = Setup();
        var c = new Customer { Code = "C1", Name = "独一名", IsActive = true };
        db.Customers.Add(c);
        await db.SaveChangesAsync();

        var result = await svc.UpdateAsync(c.Id, new UpdateCustomerRequest
        {
            Name = "独一名",  // 不改名
            IsActive = true,
        });

        Assert.Equal("独一名", result.Name);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateName_OnOther_Throws()
    {
        var (db, svc, _) = Setup();
        var a = new Customer { Code = "C1", Name = "客户A" };
        var b = new Customer { Code = "C2", Name = "客户B" };
        db.Customers.AddRange(a, b);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(b.Id, new UpdateCustomerRequest { Name = "客户A" }));
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        var (db, svc, _) = Setup();
        var c = new Customer { Code = "C1", Name = "待删", IsActive = true };
        db.Customers.Add(c);
        await db.SaveChangesAsync();

        await svc.DeleteAsync(c.Id);

        // 全局查询过滤器会隐藏软删记录，用 IgnoreQueryFilters 验证
        var soft = await db.Customers.IgnoreQueryFilters().FirstAsync(x => x.Id == c.Id);
        Assert.True(soft.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(() => svc.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetListAsync_AppliesFilters()
    {
        var (db, svc, _) = Setup();
        db.Customers.AddRange(
            new Customer { Code = "C1", Name = "甲客户", IsActive = true },
            new Customer { Code = "C2", Name = "乙客户", IsActive = false });
        await db.SaveChangesAsync();

        var result = await svc.GetListAsync("甲", null, null, 1, 10);
        Assert.Single(result.Items);
        Assert.Equal("甲客户", result.Items[0].Name);
    }

    [Fact]
    public async Task GetListAsync_ExcludesSoftDeleted()
    {
        var (db, svc, _) = Setup();
        db.Customers.AddRange(
            new Customer { Code = "C1", Name = "可见", IsActive = true },
            new Customer { Code = "C2", Name = "已删", IsActive = true, IsDeleted = true });
        await db.SaveChangesAsync();

        var result = await svc.GetListAsync(null, null, null, 1, 10);
        Assert.Single(result.Items);
        Assert.Equal("可见", result.Items[0].Name);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (_, svc, _) = Setup();
        var result = await svc.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }
}

/// <summary>编号服务测试桩：返回固定编号，不碰事务/行锁。</summary>
file class FakeNumberingService : INumberingService
{
    public string NextCode { get; set; } = "CUST-0001";
    public Task<string> GenerateAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
        => Task.FromResult(NextCode);
    public Task<string?> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
        => Task.FromResult<string?>(NextCode);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `cd backend && dotnet test OneCup.sln --filter "FullyQualifiedName~CustomerServiceTests"`
Expected: FAIL（编译错误：`ICustomerService`/`CustomerService` 类型不存在——测试引用了尚未创建的服务）

> 注：测试文件已包含 `using OneCup.Infrastructure.Persistence;`（`Repository<T>`/`UnitOfWork`）和 `using OneCup.Domain.Entities;`（`Customer`）。`file class` 是 C# 11 文件范围类型，测试桩放在文件底部即可。

- [ ] **Step 3: 创建 ICustomerService 接口**

Create `backend/src/OneCup.Application/Interfaces/ICustomerService.cs`:

```csharp
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface ICustomerService
{
    Task<PagedResult<CustomerListItemDto>> GetListAsync(
        string? keyword, string? code, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);

    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 4: 创建 CustomerService 实现**

Create `backend/src/OneCup.Application/Services/CustomerService.cs`:

```csharp
using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 客户管理服务实现。
/// CreateAsync 在事务内取号+落库（B+ 不跳号：计数器增量与客户记录同生共死）。
/// 名称唯一性预检用 AnyIgnoringFiltersAsync，识别已软删除占用的名称。
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly IRepository<Customer> _customers;
    private readonly IUnitOfWork _uow;
    private readonly INumberingService _numbering;
    private readonly IValidator<CreateCustomerRequest> _createValidator;
    private readonly IValidator<UpdateCustomerRequest> _updateValidator;

    public CustomerService(
        IRepository<Customer> customers,
        IUnitOfWork uow,
        INumberingService numbering,
        IValidator<CreateCustomerRequest> createValidator,
        IValidator<UpdateCustomerRequest> updateValidator)
    {
        _customers = customers;
        _uow = uow;
        _numbering = numbering;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<CustomerListItemDto>> GetListAsync(
        string? keyword, string? code, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _customers.CountAsync(new CustomerFilterSpec(keyword, code, isActive), ct);
        var customers = await _customers.ListAsync(
            new CustomerPagedSpec(keyword, code, isActive, page, pageSize), ct);

        return new PagedResult<CustomerListItemDto>
        {
            Items = customers.Select(c => new CustomerListItemDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                ShortName = c.ShortName,
                ContactPerson = c.ContactPerson,
                ContactPhone = c.ContactPhone,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _customers.FirstOrDefaultAsync(new CustomerByIdSpec(id), ct);
        if (c is null) return null;

        return new CustomerDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            ShortName = c.ShortName,
            ContactPerson = c.ContactPerson,
            ContactPhone = c.ContactPhone,
            Remark = c.Remark,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
        };
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        // 名称唯一性预检（绕过软删除过滤器）
        if (await _customers.AnyIgnoringFiltersAsync(new CustomerByNameSpec(request.Name), ct))
        {
            throw new DomainException($"客户名称「{request.Name}」已存在");
        }

        Guid createdId = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            // 事务内取号（行锁），计数器增量与客户记录一起提交
            var code = await _numbering.GenerateAsync(NumberTargetTypes.Customer, null, ct);
            var customer = new Customer
            {
                Code = code,
                Name = request.Name,
                ShortName = request.ShortName,
                ContactPerson = request.ContactPerson,
                ContactPhone = request.ContactPhone,
                Remark = request.Remark,
                IsActive = request.IsActive,
            };
            await _customers.AddAsync(customer, ct);
            await _uow.SaveChangesAsync(ct);
            createdId = customer.Id;
        }, ct);

        return await GetByIdAsync(createdId, ct) ?? throw new DomainException("客户创建失败");
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);

        var customer = await _customers.FirstOrDefaultAsync(new CustomerByIdSpec(id), ct)
            ?? throw new DomainException("客户不存在");

        // 改名查重（排除自身）
        if (await _customers.AnyIgnoringFiltersAsync(new CustomerByNameSpec(request.Name, id), ct))
        {
            throw new DomainException($"客户名称「{request.Name}」已存在");
        }

        customer.Name = request.Name;
        customer.ShortName = request.ShortName;
        customer.ContactPerson = request.ContactPerson;
        customer.ContactPhone = request.ContactPhone;
        customer.Remark = request.Remark;
        customer.IsActive = request.IsActive;

        await _uow.SaveChangesAsync(ct);  // 无编号操作，不需事务
        return await GetByIdAsync(id, ct) ?? throw new DomainException("客户更新失败");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await _customers.FirstOrDefaultAsync(new CustomerByIdSpec(id), ct)
            ?? throw new DomainException("客户不存在");

        customer.IsDeleted = true;  // 幂等软删
        await _uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 5: 运行测试确认通过**

Run: `cd backend && dotnet test OneCup.sln --filter "FullyQualifiedName~CustomerServiceTests"`
Expected: 10 passed, 0 failed

- [ ] **Step 6: 提交**

```bash
git add backend/src/OneCup.Application/Interfaces/ICustomerService.cs \
        backend/src/OneCup.Application/Services/CustomerService.cs \
        backend/tests/OneCup.UnitTests/Customer/CustomerServiceTests.cs
git commit -m "feat(customer): ICustomerService + CustomerService（事务化创建/CRUD）+ 测试"
```

---

## Task 7: 全量后端测试回归

**Files:** 无新建，仅运行验证

- [ ] **Step 1: 全量后端测试**

Run: `cd backend && dotnet test OneCup.sln`
Expected: 全绿（原有测试 + 新增 Customer 测试均通过，无回归）

- [ ] **Step 2: 确认无回归报错**

若有失败，定位是否因 DbContext 改动影响其他测试（如 Seed 顺序）。客户模块不应影响 Seed()，若受影响说明误改了 Seed()，回退 Task 1 的 DbContext 改动重做。

---

## Task 8: CustomersController + 授权策略 + DI 注册

**Files:**
- Create: `backend/src/OneCup.Api/Controllers/CustomersController.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`（追加授权策略 + AddScoped）

**Interfaces:**
- Consumes: `ICustomerService`（Task 6）
- Produces: 5 个 HTTP 端点（GET 列表/GET 详情/POST/PUT/DELETE）

- [ ] **Step 1: 创建控制器**

Create `backend/src/OneCup.Api/Controllers/CustomersController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Api.Filters;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 客户管理端点。类级需 customer:read；写操作叠加 customer:write。
/// </summary>
[ApiController]
[Route("api/customers")]
[Authorize(Policy = "customer-read")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword,
        [FromQuery] string? code,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _customerService.GetListAsync(keyword, code, isActive, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var customer = await _customerService.GetByIdAsync(id, ct);
        return customer is null ? NotFound() : Ok(customer);
    }

    [Audit(Module = "Customer", Action = "Create", TargetType = "Customer")]
    [Authorize(Policy = "customer-write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var customer = await _customerService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    [Audit(Module = "Customer", Action = "Update", TargetType = "Customer")]
    [Authorize(Policy = "customer-write")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken ct)
    {
        var customer = await _customerService.UpdateAsync(id, request, ct);
        return Ok(customer);
    }

    [Audit(Module = "Customer", Action = "Delete", TargetType = "Customer")]
    [Authorize(Policy = "customer-write")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _customerService.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

- [ ] **Step 2: 追加授权策略到 Program.cs**

Modify `backend/src/OneCup.Api/Program.cs`：找到 `AddAuthorization` 块（含 `options.AddPolicy("audit-view", ...)` 那段），在其内部末尾追加两条策略：

```csharp
    options.AddPolicy("customer-read", policy =>
        policy.RequireClaim("perm_codes", "customer:read"));
    options.AddPolicy("customer-write", policy =>
        policy.RequireClaim("perm_codes", "customer:write"));
```

- [ ] **Step 3: 追加 DI 注册到 Program.cs**

在同一 `Program.cs` 的 `AddScoped` 注册区（如 `builder.Services.AddScoped<IRoleService, RoleService>();` 附近）末尾追加：

```csharp
builder.Services.AddScoped<ICustomerService, CustomerService>();
```

- [ ] **Step 4: 验证编译 + 全量测试**

Run: `cd backend && dotnet build OneCup.sln && dotnet test OneCup.sln`
Expected: BUILD SUCCEEDED + 全测试通过

- [ ] **Step 5: 提交**

```bash
git add backend/src/OneCup.Api/Controllers/CustomersController.cs \
        backend/src/OneCup.Api/Program.cs
git commit -m "feat(customer): CustomersController + 授权策略 + DI 注册"
```

---

## Task 9: 前端 API 模块

**Files:**
- Create: `frontend/src/api/customer.ts`

**Interfaces:**
- Produces: `getCustomers`/`getCustomer`/`createCustomer`/`updateCustomer`/`deleteCustomer`（供 Task 11 页面使用）

- [ ] **Step 1: 创建 API 模块**

Create `frontend/src/api/customer.ts`:

```typescript
import request from './request';

// ── 类型 ──
export interface CustomerListItem {
  id: string;
  code: string;
  name: string;
  shortName?: string;
  contactPerson?: string;
  contactPhone?: string;
  isActive: boolean;
  createdAt: string;
}

export interface CustomerDetail extends CustomerListItem {
  remark?: string;
  updatedAt?: string;
}

export interface CustomerPagedResult {
  items: CustomerListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CustomerQuery {
  keyword?: string;
  code?: string;
  isActive?: boolean;
  page: number;
  pageSize: number;
}

export interface CustomerFormData {
  name: string;
  shortName?: string;
  contactPerson?: string;
  contactPhone?: string;
  remark?: string;
  isActive: boolean;
}

// ── API ──
export const getCustomers = (params: CustomerQuery) =>
  request.get<CustomerPagedResult>('/api/customers', { params });

export const getCustomer = (id: string) =>
  request.get<CustomerDetail>(`/api/customers/${id}`);

export const createCustomer = (data: CustomerFormData) =>
  request.post<CustomerDetail>('/api/customers', data);

export const updateCustomer = (id: string, data: CustomerFormData) =>
  request.put<CustomerDetail>(`/api/customers/${id}`, data);

export const deleteCustomer = (id: string) =>
  request.delete(`/api/customers/${id}`);
```

> 注：`request.ts` 默认导出是配置好拦截器的 axios 实例（`export default request`，已含 baseURL 和 JWT 拦截）。泛型返回数据体——确认响应拦截器返回 `response.data`（检查 `request.ts:35` 的 response interceptor）。若拦截器返回完整 AxiosResponse，则调用方需 `.then(r => r.data)`。

- [ ] **Step 2: 验证前端类型检查**

Run: `cd frontend && npx tsc --noEmit`
Expected: 无类型错误

- [ ] **Step 3: 提交**

```bash
git add frontend/src/api/customer.ts
git commit -m "feat(customer): 前端 API 模块"
```

---

## Task 10: 前端菜单 + 路由 + 全局文案

**Files:**
- Modify: `frontend/src/routes.ts`
- Modify: `frontend/src/router.tsx`
- Modify: `frontend/src/locale/index.ts`

- [ ] **Step 1: routes.ts 新增 business 菜单组**

Modify `frontend/src/routes.ts`：在 `export const routes: IRoute[] = [` 之后、现有 `menu.system` 对象之前，插入 business 分组：

```typescript
  {
    name: 'menu.business',
    key: 'business',
    children: [
      {
        name: 'menu.business.customer',
        key: 'business/customer',
        requiredPermissions: [
          { resource: 'customer', actions: ['read'] },
        ],
      },
    ],
  },
```

- [ ] **Step 2: router.tsx 新增路由**

Modify `frontend/src/router.tsx`：

在文件顶部 lazy import 区追加：
```typescript
const CustomerPage = lazy(() => import('@/pages/business/customer'));
```

在 `children` 数组中 `{ index: true, ... }` 之后、`system/user` 之前，插入：
```typescript
      {
        path: 'business/customer',
        element: withSuspense(
          <RequirePermission resource="customer" actions={['read']}>
            <CustomerPage />
          </RequirePermission>
        ),
      },
```

- [ ] **Step 3: locale/index.ts 新增文案**

Modify `frontend/src/locale/index.ts`：

在 `en-US` 对象内追加：
```typescript
    'menu.business': 'Business',
    'menu.business.customer': 'Customer',
```

在 `zh-CN` 对象内追加：
```typescript
    'menu.business': '业务管理',
    'menu.business.customer': '客户',
```

- [ ] **Step 4: 验证前端构建（此时页面文件未建，会报导入错误，属预期，跳到 Task 11 建完再验）**

先不构建，继续 Task 11。

- [ ] **Step 5: 暂不提交，与 Task 11 一起提交**

---

## Task 11: 前端客户页面（列表 + Modal + Drawer）

**Files:**
- Create: `frontend/src/pages/business/customer/index.tsx`
- Create: `frontend/src/pages/business/customer/form.tsx`
- Create: `frontend/src/pages/business/customer/detail.tsx`
- Create: `frontend/src/pages/business/customer/locale/index.ts`
- Create: `frontend/src/pages/business/customer/style/index.module.less`

- [ ] **Step 1: 创建样式文件**

先确认模板对应的 less 文件内容。参考 `docs/specs/templates/query-table-page.template.tsx` 引用的 `./style/index.module.less`。

Create `frontend/src/pages/business/customer/style/index.module.less`:

```less
.search-form-wrapper {
  display: flex;
  margin-bottom: 16px;
}

.search-form {
  flex: 1;
}

.right-button {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  padding-left: 24px;
  border-left: 1px solid var(--color-neutral-3);

  button {
    margin-left: 8px;
  }
}

.button-group {
  display: flex;
  justify-content: space-between;
  margin-bottom: 16px;
}
```

> 注：若 `numbering` 页面已有等价 less，可复制其 `index.module.less` 内容。该样式源自 query-table 标准设计。

- [ ] **Step 2: 创建页面级 locale**

Create `frontend/src/pages/business/customer/locale/index.ts`:

```typescript
import zhCN from './zh-CN';
import enUS from './en-US';

export default {
  'zh-CN': zhCN,
  'en-US': enUS,
};
```

Create `frontend/src/pages/business/customer/locale/zh-CN.ts`:

```typescript
export default {
  'customer.title': '客户管理',
  'customer.search.name': '客户名称',
  'customer.search.code': '客户编号',
  'customer.search.status': '启用状态',
  'customer.column.code': '客户编号',
  'customer.column.name': '客户名称',
  'customer.column.shortName': '简称',
  'customer.column.contactPerson': '联系人',
  'customer.column.contactPhone': '联系电话',
  'customer.column.status': '状态',
  'customer.column.createdAt': '创建时间',
  'customer.column.operations': '操作',
  'customer.active': '启用',
  'customer.inactive': '停用',
  'customer.button.create': '新建客户',
  'customer.button.search': '查询',
  'customer.button.reset': '重置',
  'customer.button.view': '查看',
  'customer.button.edit': '编辑',
  'customer.button.delete': '删除',
  'customer.form.title.create': '新建客户',
  'customer.form.title.edit': '编辑客户',
  'customer.form.name': '客户名称',
  'customer.form.shortName': '客户简称',
  'customer.form.contactPerson': '联系人',
  'customer.form.contactPhone': '联系电话',
  'customer.form.remark': '备注',
  'customer.form.isActive': '启用状态',
  'customer.detail.title': '客户详情',
  'customer.message.deleteOk': '确定删除该客户吗？',
  'customer.message.deleteSuccess': '删除成功',
  'customer.message.createSuccess': '创建成功',
  'customer.message.updateSuccess': '更新成功',
  'customer.error.noNumberingRule': '请先在编号管理为客户配置启用规则',
};
```

Create `frontend/src/pages/business/customer/locale/en-US.ts`:

```typescript
export default {
  'customer.title': 'Customer',
  'customer.search.name': 'Name',
  'customer.search.code': 'Code',
  'customer.search.status': 'Status',
  'customer.column.code': 'Code',
  'customer.column.name': 'Name',
  'customer.column.shortName': 'Short Name',
  'customer.column.contactPerson': 'Contact',
  'customer.column.contactPhone': 'Phone',
  'customer.column.status': 'Status',
  'customer.column.createdAt': 'Created At',
  'customer.column.operations': 'Operations',
  'customer.active': 'Active',
  'customer.inactive': 'Inactive',
  'customer.button.create': 'New Customer',
  'customer.button.search': 'Search',
  'customer.button.reset': 'Reset',
  'customer.button.view': 'View',
  'customer.button.edit': 'Edit',
  'customer.button.delete': 'Delete',
  'customer.form.title.create': 'New Customer',
  'customer.form.title.edit': 'Edit Customer',
  'customer.form.name': 'Name',
  'customer.form.shortName': 'Short Name',
  'customer.form.contactPerson': 'Contact',
  'customer.form.contactPhone': 'Phone',
  'customer.form.remark': 'Remark',
  'customer.form.isActive': 'Active',
  'customer.detail.title': 'Customer Detail',
  'customer.message.deleteOk': 'Delete this customer?',
  'customer.message.deleteSuccess': 'Deleted',
  'customer.message.createSuccess': 'Created',
  'customer.message.updateSuccess': 'Updated',
  'customer.error.noNumberingRule': 'Please configure an active numbering rule for customer first',
};
```

- [ ] **Step 3: 创建详情 Drawer 组件**

Create `frontend/src/pages/business/customer/detail.tsx`:

```typescript
import { Descriptions, Drawer } from '@arco-design/web-react';
import { CustomerDetail } from '@/api/customer';
import useLocale from '@/utils/useLocale';
import locale from './locale';

export default function CustomerDetailDrawer({
  visible,
  data,
  onClose,
}: {
  visible: boolean;
  data: CustomerDetail | null;
  onClose: () => void;
}) {
  const t = useLocale(locale);
  return (
    <Drawer
      title={t('customer.detail.title')}
      visible={visible}
      onCancel={onClose}
      footer={null}
      width={480}
    >
      {data && (
        <Descriptions
          column={1}
          data={[
            { label: t('customer.column.code'), value: data.code },
            { label: t('customer.column.name'), value: data.name },
            { label: t('customer.column.shortName'), value: data.shortName || '-' },
            { label: t('customer.column.contactPerson'), value: data.contactPerson || '-' },
            { label: t('customer.column.contactPhone'), value: data.contactPhone || '-' },
            { label: t('customer.form.remark'), value: data.remark || '-' },
            {
              label: t('customer.column.status'),
              value: data.isActive ? t('customer.active') : t('customer.inactive'),
            },
            { label: t('customer.column.createdAt'), value: data.createdAt },
          ]}
        />
      )}
    </Drawer>
  );
}
```

- [ ] **Step 4: 创建新建/编辑 Modal 表单**

Create `frontend/src/pages/business/customer/form.tsx`:

```typescript
import { useEffect, useState } from 'react';
import {
  Alert,
  Form,
  Input,
  Message,
  Modal,
  Switch,
} from '@arco-design/web-react';
import {
  CustomerDetail,
  CustomerFormData,
  createCustomer,
  updateCustomer,
} from '@/api/customer';
import useLocale from '@/utils/useLocale';
import locale from './locale';

const FormItem = Form.Item;
const TextArea = Input.TextArea;

export default function CustomerFormModal({
  visible,
  editing,
  onClose,
  onSuccess,
}: {
  visible: boolean;
  editing: CustomerDetail | null; // null = 新建模式
  onClose: () => void;
  onSuccess: () => void;
}) {
  const t = useLocale(locale);
  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  useEffect(() => {
    if (visible) {
      setErrorMsg('');
      if (editing) {
        form.setFieldsValue({
          name: editing.name,
          shortName: editing.shortName,
          contactPerson: editing.contactPerson,
          contactPhone: editing.contactPhone,
          remark: editing.remark,
          isActive: editing.isActive,
        });
      } else {
        form.resetFields();
        form.setFieldValue('isActive', true);
      }
    }
  }, [visible, editing, form]);

  const handleOk = async () => {
    try {
      const values = (await form.validate()) as CustomerFormData;
      setConfirmLoading(true);
      setErrorMsg('');
      if (editing) {
        await updateCustomer(editing.id, values);
        Message.success(t('customer.message.updateSuccess'));
      } else {
        await createCustomer(values);
        Message.success(t('customer.message.createSuccess'));
      }
      onSuccess();
      onClose();
    } catch (err: any) {
      // 后端 400：名称重复 / 无编号规则，展示在顶部 Alert
      const msg = err?.response?.data?.message || err?.message || '';
      if (msg.includes('编号') || msg.includes('rule') || msg.includes('numbering')) {
        setErrorMsg(t('customer.error.noNumberingRule'));
      } else {
        setErrorMsg(msg);
      }
    } finally {
      setConfirmLoading(false);
    }
  };

  return (
    <Modal
      title={editing ? t('customer.form.title.edit') : t('customer.form.title.create')}
      visible={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={confirmLoading}
      unmountOnExit
    >
      {errorMsg && <Alert type="error" content={errorMsg} style={{ marginBottom: 16 }} />}
      <Form form={form} layout="vertical">
        <FormItem label={t('customer.form.name')} field="name" rules={[{ required: true }]}>
          <Input maxLength={100} />
        </FormItem>
        <FormItem label={t('customer.form.shortName')} field="shortName">
          <Input maxLength={50} />
        </FormItem>
        <FormItem label={t('customer.form.contactPerson')} field="contactPerson">
          <Input maxLength={50} />
        </FormItem>
        <FormItem label={t('customer.form.contactPhone')} field="contactPhone">
          <Input maxLength={30} placeholder="0755-12345678 / 13800138000" />
        </FormItem>
        <FormItem label={t('customer.form.remark')} field="remark">
          <TextArea maxLength={500} />
        </FormItem>
        <FormItem label={t('customer.form.isActive')} field="isActive" triggerPropName="checked">
          <Switch />
        </FormItem>
      </Form>
    </Modal>
  );
}
```

- [ ] **Step 5: 创建列表页（从模板复制改造）**

Create `frontend/src/pages/business/customer/index.tsx`:

> 复制 `docs/specs/templates/query-table-page.template.tsx` 骨架，按【替换点】改造。严格遵守 query-table 标准（单 Card、三列 Form、按钮外侧 flex、工具栏 space-between）。

```typescript
import { useEffect, useMemo, useState } from 'react';
import {
  Badge,
  Button,
  Card,
  Form,
  Grid,
  Input,
  Message,
  Select,
  Space,
  Table,
  Typography,
} from '@arco-design/web-react';
import { IconPlus, IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import { CustomerListItem, deleteCustomer, getCustomers } from '@/api/customer';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import locale from './locale';
import styles from './style/index.module.less';
import CustomerFormModal from './form';
import CustomerDetailDrawer from './detail';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const Option = Select.Option;

function SearchForm({ onSearch }: { onSearch: (v: Record<string, any>) => void }) {
  const [form] = Form.useForm();
  const t = useLocale(locale);

  const handleSubmit = () => onSearch(form.getFieldsValue());
  const handleReset = () => {
    form.resetFields();
    onSearch({});
  };

  return (
    <div className={styles['search-form-wrapper']}>
      <Form
        form={form}
        className={styles['search-form']}
        labelAlign="left"
        labelCol={{ span: 7 }}
        wrapperCol={{ span: 17 }}
      >
        <Row gutter={24}>
          <Col span={8}>
            <FormItem label={t('customer.search.name')} field="keyword">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t('customer.search.code')} field="code">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t('customer.search.status')} field="isActive">
              <Select allowClear>
                <Option value={true}>{t('customer.active')}</Option>
                <Option value={false}>{t('customer.inactive')}</Option>
              </Select>
            </FormItem>
          </Col>
        </Row>
      </Form>
      <div className={styles['right-button']}>
        <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>
          {t('customer.button.search')}
        </Button>
        <Button icon={<IconRefresh />} onClick={handleReset}>
          {t('customer.button.reset')}
        </Button>
      </div>
    </div>
  );
}

export default function CustomerPage() {
  const t = useLocale(locale);
  const [data, setData] = useState<CustomerListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [formParams, setFormParams] = useState<Record<string, any>>({});
  const [pagination, setPagination] = useState({
    sizeCanChange: true,
    showTotal: true,
    pageSize: 10,
    current: 1,
    total: 0,
    pageSizeChangeResetCurrent: true,
  });

  const [formVisible, setFormVisible] = useState(false);
  const [editing, setEditing] = useState<any>(null);
  const [detailVisible, setDetailVisible] = useState(false);
  const [detailData, setDetailData] = useState<any>(null);

  const columns = useMemo(
    () => [
      { title: t('customer.column.code'), dataIndex: 'code' },
      { title: t('customer.column.name'), dataIndex: 'name' },
      { title: t('customer.column.shortName'), dataIndex: 'shortName' },
      { title: t('customer.column.contactPerson'), dataIndex: 'contactPerson' },
      { title: t('customer.column.contactPhone'), dataIndex: 'contactPhone' },
      {
        title: t('customer.column.status'),
        dataIndex: 'isActive',
        render: (v: boolean) => (
          <Badge status={v ? 'success' : 'default'} text={v ? t('customer.active') : t('customer.inactive')} />
        ),
      },
      { title: t('customer.column.createdAt'), dataIndex: 'createdAt' },
      {
        title: t('customer.column.operations'),
        dataIndex: 'operations',
        render: (_: any, record: CustomerListItem) => (
          <Space>
            <Button type="text" size="small" onClick={() => openDetail(record)}>
              {t('customer.button.view')}
            </Button>
            <PermissionWrapper requiredPermissions={[{ resource: 'customer', actions: ['write'] }]}>
              <Button type="text" size="small" onClick={() => openEdit(record)}>
                {t('customer.button.edit')}
              </Button>
              <Button type="text" size="small" status="danger" onClick={() => handleDelete(record)}>
                {t('customer.button.delete')}
              </Button>
            </PermissionWrapper>
          </Space>
        ),
      },
    ],
    [t]
  );

  function fetchData() {
    setLoading(true);
    getCustomers({ page: pagination.current, pageSize: pagination.pageSize, ...formParams })
      .then((res: any) => {
        const result = res.data || res;
        setData(result.items || []);
        setPagination((p) => ({ ...p, total: result.total || 0 }));
      })
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    fetchData();
  }, [pagination.current, pagination.pageSize, JSON.stringify(formParams)]);

  function handleSearch(params: Record<string, any>) {
    setPagination((p) => ({ ...p, current: 1 }));
    setFormParams(params);
  }

  function onChangeTable({ current, pageSize }: any) {
    setPagination((p) => ({ ...p, current, pageSize }));
  }

  function openCreate() {
    setEditing(null);
    setFormVisible(true);
  }
  function openEdit(record: CustomerListItem) {
    setEditing(record);
    setFormVisible(true);
  }
  function openDetail(record: CustomerListItem) {
    setDetailData(record);
    setDetailVisible(true);
  }
  function handleDelete(record: CustomerListItem) {
    Modal.confirm({
      title: t('customer.message.deleteOk'),
      onOk: async () => {
        await deleteCustomer(record.id);
        Message.success(t('customer.message.deleteSuccess'));
        fetchData();
      },
    });
  }

  return (
    <Card>
      <Title heading={6}>{t('customer.title')}</Title>
      <SearchForm onSearch={handleSearch} />
      <div className={styles['button-group']}>
        <Space>
          <PermissionWrapper requiredPermissions={[{ resource: 'customer', actions: ['write'] }]}>
            <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
              {t('customer.button.create')}
            </Button>
          </PermissionWrapper>
        </Space>
        <Space />
      </div>
      <Table
        rowKey="id"
        loading={loading}
        onChange={onChangeTable}
        pagination={pagination}
        columns={columns}
        data={data}
      />
      <CustomerFormModal
        visible={formVisible}
        editing={editing}
        onClose={() => setFormVisible(false)}
        onSuccess={fetchData}
      />
      <CustomerDetailDrawer
        visible={detailVisible}
        data={detailData}
        onClose={() => setDetailVisible(false)}
      />
    </Card>
  );
}
```

> **PermissionWrapper API**：该组件接收 `requiredPermissions` 数组属性（非 `resource`/`actions` 独立属性），用法为 `<PermissionWrapper requiredPermissions={[{ resource: 'customer', actions: ['write'] }]}>`（见 `components/PermissionWrapper/index.tsx`）。路由级用 `RequirePermission`（接收 `resource`/`actions`），按钮级用 `PermissionWrapper`。

- [ ] **Step 6: 验证前端构建**

Run: `cd frontend && npm run build`
Expected: 构建成功，无 TS 错误

- [ ] **Step 7: 提交（含 Task 10 的 routes/router/locale 改动）**

```bash
git add frontend/src/routes.ts frontend/src/router.tsx frontend/src/locale/index.ts \
        frontend/src/pages/business/customer/
git commit -m "feat(customer): 前端客户页面（列表+Modal+Drawer）+ 菜单路由"
```

---

## Task 12: 验收与最终回归

**Files:** 无新建

- [ ] **Step 1: 后端全量构建 + 测试**

Run:
```bash
cd backend
dotnet build OneCup.sln
dotnet test OneCup.sln
```
Expected: BUILD SUCCEEDED + 全测试通过

- [ ] **Step 2: 前端构建**

Run: `cd frontend && npm run build`
Expected: 构建成功

- [ ] **Step 3: 协作约定核对（对照 spec 第 6.1 节）**

人工/命令核对：
- `git diff main -- backend/src/OneCup.Infrastructure/Persistence/SeedData.cs` 应**无改动**（不碰种子）
- `grep -n "HasData" backend/src/OneCup.Infrastructure/Migrations/*_AddCustomerModule.cs` 应**无输出**（迁移不含种子）
- `grep -rn "0000000001[0-9][0-9]" backend/src/` 应只在 main 基线文件出现（未新增 Guid）
- 确认未修改 unit/color 相关文件

- [ ] **Step 4: 提交验收记录（可选）**

如需记录验收，创建 commit：
```bash
git commit --allow-empty -m "chore(customer): 验收通过（build + test 全绿，协作约定核对无误）"
```

---

## 实施顺序总结

Task 1 → 2（领域+迁移）→ 3 → 4（DTO+规范）→ 5（校验，TDD）→ 6（服务，TDD）→ 7（回归）→ 8（控制器+DI）→ 9（前端 API）→ 10+11（前端页面）→ 12（验收）。
