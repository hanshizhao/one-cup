# 工序管理（Process Management）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 从零构建工序管理模块——带自动编号、软删除的业务对象（与客户同构），含全栈后端 + 前端 + 测试。

**Architecture:** 分层架构（Domain → Application → Infrastructure → Api），与 Customer 模块同构。`ProcessService.CreateAsync` 在事务内调编号引擎 `GenerateAsync(NumberTargetTypes.Process, ...)`；软删除 + 名称「分类内唯一」；前端列表页走列表查询页标准，创建表单走 Convention c02（previewCode 流程），删除走 Convention c01（Popconfirm）。

**Tech Stack:** 后端 .NET 10 / EF Core 10 / PostgreSQL / FluentValidation / xUnit(InMemory)；前端 React + Arco Design + axios；并行分支 `feat/process-mgmt`。

## Global Constraints

- **种子 Guid（合同 §3.1，独占，禁止占用他段）**：权限 `PermProcessRead=00000000-0000-0000-0000-00000000032b` / `Create=...032c` / `Update=...032d` / `Delete=...032e`；目标类型 `TargetTypeProcess=00000000-0000-0000-0000-000000000207`。不动 `301-32a` / `0201-0206`。
- **迁移名**：`AddProcessModule`（命令：`dotnet ef migrations add AddProcessModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api`，在 `backend/` 下执行）。
- **Name 唯一性**：分类内唯一（同 Category 唯一，跨 Category 可同名）。应用层 spec 兜底 Category=NULL；DB 用复合唯一索引 `HasIndex(Name, Category).IsUnique()`。
- **列表排序**：`ApplyOrderBy(SortOrder)` 单字段（Specification 基类无 ThenBy）。
- **developer 角色**：种 `process:read`（与 material/customer/color 看齐）；admin 走通配 `*` 不加 role_permissions。
- **EF 配置**：`ApplyConfigurationsFromAssembly` 自动扫描，**禁**手写 `ApplyConfiguration`。
- **不改共享基类**：不改 `Specification<T>`、Redux store、api 目录聚合。
- **参考实现**：Customer（全栈同构）、Color（previewCode 流程 + SortOrder 排序）。设计 spec：`docs/superpowers/specs/2026-07-04-process-management-design.md`。
- **命名空间别名**：`using ProcessEntity = OneCup.Domain.Entities.Process;`（避免与 `System.Diagnostics.Process` 冲突，凡 `OneCup.UnitTests.Process` 命名空间内的测试必须用别名）。

---

## File Structure

**后端 per-file 新增：**
- `backend/src/OneCup.Domain/Entities/Process.cs` — 实体
- `backend/src/OneCup.Infrastructure/Persistence/Configurations/ProcessConfiguration.cs` — EF 配置
- `backend/src/OneCup.Application/Dtos/System/ProcessDtos.cs` — DTO
- `backend/src/OneCup.Application/Interfaces/IProcessService.cs` — 服务接口
- `backend/src/OneCup.Application/Services/ProcessService.cs` — 服务实现
- `backend/src/OneCup.Application/Specifications/ProcessSpecs.cs` — 查询规范
- `backend/src/OneCup.Application/Validators/System/CreateProcessRequestValidator.cs` + `UpdateProcessRequestValidator.cs`
- `backend/src/OneCup.Api/Controllers/ProcessesController.cs` — API
- `backend/src/OneCup.Infrastructure/Migrations/<ts>_AddProcessModule.cs`(+`.Designer.cs`) — EF 迁移（自动生成）
- `backend/tests/OneCup.UnitTests/Process/ProcessServiceTests.cs` + `ProcessValidatorTests.cs`

**后端共享修改（仅本任务，按合同）：**
- `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs` — +5 Guid 常量
- `backend/src/OneCup.Application/Common/NumberTargetTypes.cs` — +Process 常量
- `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs` — +DbSet + Seed()
- `backend/src/OneCup.Api/Program.cs` — +AddScoped + 4 AddPolicy

**前端 per-file 新增：**
- `frontend/src/api/process.ts`
- `frontend/src/pages/business/process/index.tsx` / `form.tsx` / `detail.tsx`
- `frontend/src/pages/business/process/style/index.module.less`
- `frontend/src/pages/business/process/locale/index.ts` / `zh-CN.ts` / `en-US.ts`

**前端共享修改：**
- `frontend/src/routes.ts` / `router.tsx` / `locale/index.ts`（business 域末尾追加）

---

### Task 1: 后端实体 + EF 配置 + 种子常量

**Files:**
- Create: `backend/src/OneCup.Domain/Entities/Process.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/ProcessConfiguration.cs`
- Modify: `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`（末尾加 5 常量）
- Modify: `backend/src/OneCup.Application/Common/NumberTargetTypes.cs`（末尾加 Process 常量）

**Interfaces:**
- Produces: `OneCup.Domain.Entities.Process`（字段见下），供 Task 3/5/8 引用；`NumberTargetTypes.Process = "process"`，供 Task 5 引用。

- [ ] **Step 1: 创建 Process 实体**

创建 `backend/src/OneCup.Domain/Entities/Process.cs`：

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 工序档案。工序编号由编号系统在创建事务内生成。
/// </summary>
public class Process : BaseEntity, ISoftDeletable
{
    /// <summary>工序编号（编号系统生成，如 PRC-0001）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>工序名称（分类内唯一）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>工序分类（前处理/染色/后整理…），可空</summary>
    public string? Category { get; set; }

    /// <summary>排序号（列表按此升序）</summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>启用状态</summary>
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; } = false;
}
```

- [ ] **Step 2: 创建 EF 配置**

创建 `backend/src/OneCup.Infrastructure/Persistence/Configurations/ProcessConfiguration.cs`（照 CustomerConfiguration 结构，字段对齐 Process；索引按设计：Code 全局唯一 + (Name,Category) 复合唯一）：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class ProcessConfiguration : IEntityTypeConfiguration<Process>
{
    public void Configure(EntityTypeBuilder<Process> builder)
    {
        builder.ToTable("processes");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(p => p.Category).HasColumnName("category").HasMaxLength(50);
        builder.Property(p => p.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(p => p.Remark).HasColumnName("remark").HasMaxLength(500);
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(p => p.Code).IsUnique();
        // 分类内唯一：同 Category 下 Name 唯一。Category=NULL 时 PG 唯一索引不拦截，
        // 由应用层 ProcessByNameSpec 兜底判空。
        builder.HasIndex(p => new { p.Name, p.Category }).IsUnique();

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
```

- [ ] **Step 3: SeedData.cs 末尾加 5 个 Guid 常量**

在 `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs` 的 `TargetTypeProduct` 行**之后**（文件末尾的 `}` 之前）追加：

```csharp
    // === Process 模块（feat/process-mgmt）===
    public static readonly Guid PermProcessRead = Guid.Parse("00000000-0000-0000-0000-00000000032b");
    public static readonly Guid PermProcessCreate = Guid.Parse("00000000-0000-0000-0000-00000000032c");
    public static readonly Guid PermProcessUpdate = Guid.Parse("00000000-0000-0000-0000-00000000032d");
    public static readonly Guid PermProcessDelete = Guid.Parse("00000000-0000-0000-0000-00000000032e");
    public static readonly Guid TargetTypeProcess = Guid.Parse("00000000-0000-0000-0000-000000000207");
```

- [ ] **Step 4: NumberTargetTypes.cs 末尾加 Process 常量**

在 `backend/src/OneCup.Application/Common/NumberTargetTypes.cs` 的 `Product` 常量行**之后**追加：

```csharp
    public const string Process = "process";
```

- [ ] **Step 5: 构建验证**

Run: `cd backend && dotnet build OneCup.sln`
Expected: BUILD SUCCEEDED（实体 + 配置 + 常量编译通过；配置由 ApplyConfigurationsFromAssembly 扫描，无需手动注册）。

- [ ] **Step 6: Commit**

```bash
git add backend/src/OneCup.Domain/Entities/Process.cs backend/src/OneCup.Infrastructure/Persistence/Configurations/ProcessConfiguration.cs backend/src/OneCup.Infrastructure/Persistence/SeedData.cs backend/src/OneCup.Application/Common/NumberTargetTypes.cs
git commit -m "feat(process): 实体 + EF配置 + 种子Guid常量"
```

---

### Task 2: DTO + Validators

**Files:**
- Create: `backend/src/OneCup.Application/Dtos/System/ProcessDtos.cs`
- Create: `backend/src/OneCup.Application/Validators/System/CreateProcessRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/UpdateProcessRequestValidator.cs`

**Interfaces:**
- Consumes: `Process`（Task 1）
- Produces: `ProcessListItemDto` / `ProcessDto` / `CreateProcessRequest` / `UpdateProcessRequest`，供 Task 3/5 引用。

- [ ] **Step 1: 创建 ProcessDtos.cs**

创建 `backend/src/OneCup.Application/Dtos/System/ProcessDtos.cs`（照 CustomerDtos 结构）：

```csharp
namespace OneCup.Application.Dtos.System;

/// <summary>工序列表项（表格行）。</summary>
public class ProcessListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>工序详情（Drawer 只读）。</summary>
public class ProcessDto : ProcessListItemDto
{
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>新建工序请求。Code 不在此处——由系统在事务内生成。</summary>
public class CreateProcessRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int SortOrder { get; set; } = 0;
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>编辑工序请求（字段同 Create，独立类以便 FluentValidation 区分规则）。</summary>
public class UpdateProcessRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int SortOrder { get; set; } = 0;
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
}
```

- [ ] **Step 2: 创建 CreateProcessRequestValidator.cs**

创建 `backend/src/OneCup.Application/Validators/System/CreateProcessRequestValidator.cs`（照 CreateCustomerRequestValidator）：

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>创建工序请求校验。Name 必填，分类内唯一性在 Service 层用 spec 预检。</summary>
public class CreateProcessRequestValidator : AbstractValidator<CreateProcessRequest>
{
    public CreateProcessRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Category).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.Category));
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
    }
}
```

- [ ] **Step 3: 创建 UpdateProcessRequestValidator.cs**

创建 `backend/src/OneCup.Application/Validators/System/UpdateProcessRequestValidator.cs`：

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>编辑工序请求校验（字段约束同 Create）。</summary>
public class UpdateProcessRequestValidator : AbstractValidator<UpdateProcessRequest>
{
    public UpdateProcessRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Category).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.Category));
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
    }
}
```

- [ ] **Step 4: 构建验证**

Run: `cd backend && dotnet build OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 5: Commit**

```bash
git add backend/src/OneCup.Application/Dtos/System/ProcessDtos.cs backend/src/OneCup.Application/Validators/System/CreateProcessRequestValidator.cs backend/src/OneCup.Application/Validators/System/UpdateProcessRequestValidator.cs
git commit -m "feat(process): DTO + FluentValidation 校验器"
```

---

### Task 3: Specifications

**Files:**
- Create: `backend/src/OneCup.Application/Specifications/ProcessSpecs.cs`

**Interfaces:**
- Consumes: `Process`（Task 1），`Specification<T>` 基类（既有）
- Produces: `ProcessFilterSpec` / `ProcessPagedSpec` / `ProcessByIdSpec` / `ProcessByNameSpec`，供 Task 5 引用。

- [ ] **Step 1: 创建 ProcessSpecs.cs**

创建 `backend/src/OneCup.Application/Specifications/ProcessSpecs.cs`（照 CustomerSpecs 结构；关键差异：keyword 匹配 Name+Code（无 ShortName）；category 精确匹配含 NULL 处理；Paged 按 SortOrder 升序单字段）：

```csharp
using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>
/// 仅按 keyword/category/isActive 过滤的工序查询规范（不含分页/排序）。
/// 专用于 CountAsync 统计总数——若用带分页的规范统计，
/// Repository.CountAsync 会应用 Skip/Take，导致只统计当前页子集。
/// 与 <see cref="ProcessPagedSpec"/> 共享相同的 Where 条件。
/// </summary>
/// <remarks>
/// 基类 ApplyCriteria 是覆盖语义，多条件必须组合为单一 predicate 后调用一次。
/// </remarks>
public class ProcessFilterSpec : Specification<Process>
{
    public ProcessFilterSpec(string? keyword, string? category, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        ApplyCriteria(p =>
            (kw == null || p.Name.Contains(kw) || p.Code.Contains(kw)) &&
            // category 精确匹配：传入非空则等值，传入空则不限（含 NULL 行）
            (cat == null || p.Category == cat) &&
            (isActive == null || p.IsActive == isActive.Value));
    }
}

/// <summary>工序分页查询（含过滤、按 SortOrder 升序单字段）。</summary>
/// <remarks>Specification 基类只支持单字段 OrderBy，无 ThenBy；SortOrder 相同时次序不稳定。</remarks>
public class ProcessPagedSpec : Specification<Process>
{
    public ProcessPagedSpec(string? keyword, string? category, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        ApplyCriteria(p =>
            (kw == null || p.Name.Contains(kw) || p.Code.Contains(kw)) &&
            (cat == null || p.Category == cat) &&
            (isActive == null || p.IsActive == isActive.Value));

        ApplyOrderBy(p => p.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>按 Id 查询工序（tracked，详情/更新用 FirstOrDefaultAsync）。</summary>
public class ProcessByIdSpec : Specification<Process>
{
    public ProcessByIdSpec(Guid id)
    {
        ApplyCriteria(p => p.Id == id);
    }
}

/// <summary>
/// 名称「分类内唯一」校验（配合 AnyIgnoringFiltersAsync，绕过软删除过滤器）。
/// 关键：Category 为 null 时显式匹配 p.Category == null，不依赖 DB 唯一索引对 NULL 的处理。
/// </summary>
public class ProcessByNameSpec : Specification<Process>
{
    public ProcessByNameSpec(string name, string? category, Guid? excludingId = null)
    {
        var cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        var exclude = excludingId;
        ApplyCriteria(p =>
            p.Name == name &&
            (cat == null ? p.Category == null : p.Category == cat) &&
            (exclude == null || p.Id != exclude.Value));
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `cd backend && dotnet build OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Application/Specifications/ProcessSpecs.cs
git commit -m "feat(process): 查询规范(分类内唯一/SortOrder排序)"
```

---

### Task 4: 服务接口

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/IProcessService.cs`

**Interfaces:**
- Consumes: DTOs（Task 2），`PagedResult<T>`（既有 `OneCup.Application.Common`）
- Produces: `IProcessService`，供 Task 5（实现）+ Task 8（Controller）+ Program.cs 注册引用。

- [ ] **Step 1: 创建 IProcessService.cs**

创建 `backend/src/OneCup.Application/Interfaces/IProcessService.cs`（照 ICustomerService）：

```csharp
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IProcessService
{
    Task<PagedResult<ProcessListItemDto>> GetListAsync(
        string? keyword, string? category, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    Task<ProcessDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<ProcessDto> CreateAsync(CreateProcessRequest request, CancellationToken ct = default);

    Task<ProcessDto> UpdateAsync(Guid id, UpdateProcessRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 2: 构建验证**

Run: `cd backend && dotnet build OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Application/Interfaces/IProcessService.cs
git commit -m "feat(process): 服务接口"
```

---

### Task 5: 服务实现

**Files:**
- Create: `backend/src/OneCup.Application/Services/ProcessService.cs`

**Interfaces:**
- Consumes: `IRepository<Process>`、`IUnitOfWork`、`INumberingService`（既有 `OneCup.Application.Interfaces`）；`IValidator<Create/UpdateProcessRequest>`（Task 2）；specs（Task 3）；`NumberTargetTypes.Process`（Task 1）；`DomainException`（`OneCup.Domain.Exceptions`）；`EnsureValidAsync` 扩展（既有）。
- Produces: `ProcessService`，供 Task 6 测试 + Task 8 Controller + Program.cs 引用。

- [ ] **Step 1: 创建 ProcessService.cs**

创建 `backend/src/OneCup.Application/Services/ProcessService.cs`（照 CustomerService 结构；关键：Create/Update 用 `ProcessByNameSpec(name, category[, id])` 查重；Create 事务内调 `GenerateAsync(NumberTargetTypes.Process, null, ct)`）：

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
/// 工序管理服务实现。
/// CreateAsync 在事务内取号+落库（计数器增量与工序记录同生共死，不跳号）。
/// 名称「分类内唯一」预检用 AnyIgnoringFiltersAsync，识别已软删除占用的名称。
/// </summary>
public class ProcessService : IProcessService
{
    private readonly IRepository<Process> _processes;
    private readonly IUnitOfWork _uow;
    private readonly INumberingService _numbering;
    private readonly IValidator<CreateProcessRequest> _createValidator;
    private readonly IValidator<UpdateProcessRequest> _updateValidator;

    public ProcessService(
        IRepository<Process> processes,
        IUnitOfWork uow,
        INumberingService numbering,
        IValidator<CreateProcessRequest> createValidator,
        IValidator<UpdateProcessRequest> updateValidator)
    {
        _processes = processes;
        _uow = uow;
        _numbering = numbering;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<ProcessListItemDto>> GetListAsync(
        string? keyword, string? category, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _processes.CountAsync(new ProcessFilterSpec(keyword, category, isActive), ct);
        var items = await _processes.ListAsync(
            new ProcessPagedSpec(keyword, category, isActive, page, pageSize), ct);

        return new PagedResult<ProcessListItemDto>
        {
            Items = items.Select(p => new ProcessListItemDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Category = p.Category,
                SortOrder = p.SortOrder,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<ProcessDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _processes.FirstOrDefaultAsync(new ProcessByIdSpec(id), ct);
        if (p is null) return null;

        return new ProcessDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Category = p.Category,
            SortOrder = p.SortOrder,
            Remark = p.Remark,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
        };
    }

    public async Task<ProcessDto> CreateAsync(CreateProcessRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        // 名称「分类内唯一」预检（绕过软删除过滤器）
        if (await _processes.AnyIgnoringFiltersAsync(
            new ProcessByNameSpec(request.Name, request.Category), ct))
        {
            throw new DomainException($"工序名称「{request.Name}」在该分类下已存在");
        }

        Guid createdId = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            // 事务内取号（行锁），计数器增量与工序记录一起提交
            var code = await _numbering.GenerateAsync(NumberTargetTypes.Process, null, ct);
            var process = new Process
            {
                Code = code,
                Name = request.Name,
                Category = request.Category,
                SortOrder = request.SortOrder,
                Remark = request.Remark,
                IsActive = request.IsActive,
            };
            await _processes.AddAsync(process, ct);
            await _uow.SaveChangesAsync(ct);
            createdId = process.Id;
        }, ct);

        return await GetByIdAsync(createdId, ct) ?? throw new DomainException("工序创建失败");
    }

    public async Task<ProcessDto> UpdateAsync(Guid id, UpdateProcessRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);

        var process = await _processes.FirstOrDefaultAsync(new ProcessByIdSpec(id), ct)
            ?? throw new DomainException("工序不存在");

        // 改名/改分类查重（排除自身）
        if (await _processes.AnyIgnoringFiltersAsync(
            new ProcessByNameSpec(request.Name, request.Category, id), ct))
        {
            throw new DomainException($"工序名称「{request.Name}」在该分类下已存在");
        }

        process.Name = request.Name;
        process.Category = request.Category;
        process.SortOrder = request.SortOrder;
        process.Remark = request.Remark;
        process.IsActive = request.IsActive;

        await _uow.SaveChangesAsync(ct);  // 无编号操作，不需事务
        return await GetByIdAsync(id, ct) ?? throw new DomainException("工序更新失败");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // GetByIdAsync 走 FindAsync（绕过 QueryFilter），已软删工序仍可找到 → 幂等重删返回 204。
        var process = await _processes.GetByIdAsync(id, ct)
            ?? throw new DomainException("工序不存在");

        process.IsDeleted = true;
        await _uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `cd backend && dotnet build OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Application/Services/ProcessService.cs
git commit -m "feat(process): 服务实现(事务内取号+分类内唯一查重)"
```

---

### Task 6: 服务层单元测试

**Files:**
- Test: `backend/tests/OneCup.UnitTests/Process/ProcessServiceTests.cs`

**Interfaces:**
- Consumes: `ProcessService`（Task 5）、specs（Task 3）、DTOs（Task 2）、`OneCupDbContext`/`Repository<>`/`UnitOfWork`（既有 Infrastructure）。**命名空间别名必填**。

- [ ] **Step 1: 创建 ProcessServiceTests.cs**

创建 `backend/tests/OneCup.UnitTests/Process/ProcessServiceTests.cs`（照 CustomerServiceTests 结构 + 自增 FakeNumberingService；注意 `using ProcessEntity = OneCup.Domain.Entities.Process;` 别名避免 `System.Diagnostics.Process` 冲突）：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Services;
using OneCup.Application.Validators.System;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
// 命名空间 OneCup.UnitTests.Process 内，未限定的 "Process" 可能被解析为
// System.Diagnostics.Process；用别名显式指向实体类型。
using ProcessEntity = OneCup.Domain.Entities.Process;

namespace OneCup.UnitTests.Process;

public class ProcessServiceTests
{
    private static (OneCupDbContext db, ProcessService svc, FakeNumberingService numbering) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"process-test-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);

        var numbering = new FakeNumberingService();
        var svc = new ProcessService(
            new Repository<ProcessEntity>(db),
            new UnitOfWork(db),
            numbering,
            new CreateProcessRequestValidator(),
            new UpdateProcessRequestValidator());
        return (db, svc, numbering);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static CreateProcessRequest ValidCreate() => new()
    {
        Name = "染色",
        Category = "前处理",
        SortOrder = 1,
    };

    // ── 新增 ──

    [Fact]
    public async Task CreateAsync_CreatesProcess_WithGeneratedCode()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "PRC-0001";

        var dto = await svc.CreateAsync(ValidCreate());
        Assert.Equal("PRC-0001", dto.Code);   // code 由编号引擎生成
        Assert.Equal("染色", dto.Name);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_InSameCategory_Throws()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate());   // 染色/前处理
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(ValidCreate() with { SortOrder = 2 }));  // 同名同分类
    }

    [Fact]
    public async Task CreateAsync_SameName_DifferentCategory_Allowed()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate() with { Category = "前处理" });   // 染色/前处理
        // 跨分类同名应允许
        var dto = await svc.CreateAsync(ValidCreate() with { Category = "后整理", SortOrder = 2 });
        Assert.Equal("染色", dto.Name);
        Assert.Equal("后整理", dto.Category);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_BothNullCategory_Throws()
    {
        // Category=NULL 的同名：DB 唯一索引不拦截，应用层 spec 兜底
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate() with { Category = null });
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(ValidCreate() with { Category = null }));
    }

    // ── 编辑 ──

    [Fact]
    public async Task UpdateAsync_CodeImmutable_FieldsUpdatable()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "PRC-0001";
        var dto = await svc.CreateAsync(ValidCreate());
        await svc.UpdateAsync(dto.Id, new UpdateProcessRequest
        {
            Name = "染色改", Category = "染色", SortOrder = 5, Remark = "备注",
        });
        var updated = await svc.GetByIdAsync(dto.Id);
        Assert.Equal("PRC-0001", updated!.Code);   // code 由引擎生成，编辑不可改
        Assert.Equal("染色改", updated.Name);
        Assert.Equal("染色", updated.Category);
        Assert.Equal(5, updated.SortOrder);
        Assert.Equal("备注", updated.Remark);
    }

    [Fact]
    public async Task UpdateAsync_RenameToExisting_InSameCategory_Throws()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate() with { Name = "染色", Category = "前处理" });
        var b = await svc.CreateAsync(ValidCreate() with { Name = "织造", Category = "前处理", SortOrder = 2 });
        // 把 b 改名成已存在的「染色/前处理」→ 应抛错
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(b.Id, new UpdateProcessRequest { Name = "染色", Category = "前处理" }));
    }

    [Fact]
    public async Task UpdateAsync_KeepOwnName_Allowed()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.CreateAsync(ValidCreate());   // 染色/前处理
        // 改其它字段但保留同名同分类 → 不应触发查重（excludingId 生效）
        await svc.UpdateAsync(dto.Id, new UpdateProcessRequest
        {
            Name = "染色", Category = "前处理", SortOrder = 9,
        });
        var updated = await svc.GetByIdAsync(dto.Id);
        Assert.Equal(9, updated!.SortOrder);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(Guid.NewGuid(), new UpdateProcessRequest { Name = "x" }));
    }

    // ── 删除（软删除 + 幂等）──

    [Fact]
    public async Task DeleteAsync_SoftDeletes_AndIdempotent()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.CreateAsync(ValidCreate());
        await svc.DeleteAsync(dto.Id);
        // 软删后 GetByIdAsync（走软删过滤器）返回 null
        Assert.Null(await svc.GetByIdAsync(dto.Id));
        // 幂等重删不抛错（GetByIdAsync 走 FindAsync 绕过滤器）
        await svc.DeleteAsync(dto.Id);   // 不抛
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(() => svc.DeleteAsync(Guid.NewGuid()));
    }

    // ── 查询 ──

    [Fact]
    public async Task GetListAsync_FiltersByKeyword()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate() with { Name = "染色" });
        await svc.CreateAsync(ValidCreate() with { Name = "织造", SortOrder = 2 });
        var res = await svc.GetListAsync("染", null, null, 1, 10);
        Assert.Single(res.Items);
        Assert.Equal(1, res.Total);
    }

    [Fact]
    public async Task GetListAsync_FiltersByCategory()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate() with { Category = "前处理" });
        await svc.CreateAsync(ValidCreate() with { Category = "前处理", Name = "织造", SortOrder = 2 });
        await svc.CreateAsync(ValidCreate() with { Category = "后整理", Name = "定型", SortOrder = 3 });
        var res = await svc.GetListAsync(null, "前处理", null, 1, 10);
        Assert.Equal(2, res.Total);
    }

    [Fact]
    public async Task GetListAsync_FiltersByIsActive()
    {
        var (_, svc, _) = Setup();
        var a = await svc.CreateAsync(ValidCreate());
        await svc.UpdateAsync(a.Id, new UpdateProcessRequest
        { Name = "染色", Category = "前处理", IsActive = false });
        await svc.CreateAsync(ValidCreate() with { Name = "织造", SortOrder = 2 });
        var res = await svc.GetListAsync(null, null, false, 1, 10);
        Assert.Equal(1, res.Total);
        Assert.Equal("染色", res.Items[0].Name);
    }

    [Fact]
    public async Task GetListAsync_TotalUnaffectedByPaging()
    {
        var (_, svc, _) = Setup();
        for (var i = 0; i < 5; i++)
            await svc.CreateAsync(ValidCreate() with
            { Name = $"工序{i}", Category = "前处理", SortOrder = i });
        var res = await svc.GetListAsync(null, "前处理", null, 1, 2);
        Assert.Equal(2, res.Items.Count);
        Assert.Equal(5, res.Total);   // total 不受分页污染
    }

    [Fact]
    public async Task GetListAsync_OrdersBySortOrder()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate() with { Name = "C", SortOrder = 3 });
        await svc.CreateAsync(ValidCreate() with { Name = "A", SortOrder = 1 });
        await svc.CreateAsync(ValidCreate() with { Name = "B", SortOrder = 2 });
        var res = await svc.GetListAsync(null, null, null, 1, 10);
        Assert.Equal("A", res.Items[0].Name);
        Assert.Equal("B", res.Items[1].Name);
        Assert.Equal("C", res.Items[2].Name);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (_, svc, _) = Setup();
        Assert.Null(await svc.GetByIdAsync(Guid.NewGuid()));
    }
}

/// <summary>
/// 编号引擎测试替身：每次 GenerateAsync 返回自增 code（PRC-0001, PRC-0002 ...）。
/// NextCode 可显式覆盖首次返回值。
/// </summary>
internal sealed class FakeNumberingService : INumberingService
{
    private int _seq;
    public string? NextCode { get; set; }

    public Task<string> GenerateAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
    {
        if (NextCode is not null)
        {
            var code = NextCode;
            NextCode = null;   // 仅首次用显式值，之后自增
            return Task.FromResult(code);
        }
        _seq++;
        return Task.FromResult($"PRC-{_seq:D4}");
    }

    public Task<string?> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
        => Task.FromResult<string?>(NextCode ?? $"PRC-{(_seq + 1):D4}");
}
```

- [ ] **Step 2: 跑测试**

Run: `cd backend && dotnet test OneCup.sln --filter "FullyQualifiedName~OneCup.UnitTests.Process.ProcessServiceTests"`
Expected: 全部 PASS（15 个测试）

- [ ] **Step 3: Commit**

```bash
git add backend/tests/OneCup.UnitTests/Process/ProcessServiceTests.cs
git commit -m "test(process): 服务层单测(分类内唯一/软删幂等/SortOrder排序)"
```

---

### Task 7: Validator 单元测试

**Files:**
- Test: `backend/tests/OneCup.UnitTests/Process/ProcessValidatorTests.cs`

- [ ] **Step 1: 创建 ProcessValidatorTests.cs**

创建 `backend/tests/OneCup.UnitTests/Process/ProcessValidatorTests.cs`：

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.Process;

public class ProcessValidatorTests
{
    private static CreateProcessRequest ValidCreate() => new()
    {
        Name = "染色",
        Category = "前处理",
        SortOrder = 1,
    };

    [Fact]
    public void Create_EmptyName_Invalid()
    {
        var result = new CreateProcessRequestValidator().Validate(ValidCreate() with { Name = "" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_NameTooLong_Invalid()
    {
        var result = new CreateProcessRequestValidator().Validate(
            ValidCreate() with { Name = new string('x', 51) });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_CategoryTooLong_Invalid()
    {
        var result = new CreateProcessRequestValidator().Validate(
            ValidCreate() with { Category = new string('y', 51) });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_RemarkTooLong_Invalid()
    {
        var result = new CreateProcessRequestValidator().Validate(
            ValidCreate() with { Remark = new string('z', 501) });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_NullCategoryAndRemark_Valid()
    {
        var result = new CreateProcessRequestValidator().Validate(
            ValidCreate() with { Category = null, Remark = null });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Update_EmptyName_Invalid()
    {
        var result = new UpdateProcessRequestValidator().Validate(
            new UpdateProcessRequest { Name = "" });
        Assert.False(result.IsValid);
    }
}
```

- [ ] **Step 2: 跑测试**

Run: `cd backend && dotnet test OneCup.sln --filter "FullyQualifiedName~OneCup.UnitTests.Process.ProcessValidatorTests"`
Expected: 全部 PASS（6 个测试）

- [ ] **Step 3: Commit**

```bash
git add backend/tests/OneCup.UnitTests/Process/ProcessValidatorTests.cs
git commit -m "test(process): Validator 单测"
```

---

### Task 8: Controller

**Files:**
- Create: `backend/src/OneCup.Api/Controllers/ProcessesController.cs`

**Interfaces:**
- Consumes: `IProcessService`（Task 4/5）、DTOs（Task 2）、`[Audit]` 过滤器（既有 `OneCup.Api.Filters`）。

- [ ] **Step 1: 创建 ProcessesController.cs**

创建 `backend/src/OneCup.Api/Controllers/ProcessesController.cs`（照 CustomersController）：

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Api.Filters;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 工序管理端点。类级需 process:read；写操作需 process:create / process:update / process:delete。
/// </summary>
[ApiController]
[Route("api/processes")]
[Authorize(Policy = "process:read")]
public class ProcessesController : ControllerBase
{
    private readonly IProcessService _processService;

    public ProcessesController(IProcessService processService)
    {
        _processService = processService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword,
        [FromQuery] string? category,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _processService.GetListAsync(keyword, category, isActive, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var process = await _processService.GetByIdAsync(id, ct);
        return process is null ? NotFound() : Ok(process);
    }

    [Audit(Module = "Process", Action = "Create", TargetType = "Process")]
    [Authorize(Policy = "process:create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProcessRequest request, CancellationToken ct)
    {
        var process = await _processService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = process.Id }, process);
    }

    [Audit(Module = "Process", Action = "Update", TargetType = "Process")]
    [Authorize(Policy = "process:update")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProcessRequest request, CancellationToken ct)
    {
        var process = await _processService.UpdateAsync(id, request, ct);
        return Ok(process);
    }

    [Audit(Module = "Process", Action = "Delete", TargetType = "Process")]
    [Authorize(Policy = "process:delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _processService.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

- [ ] **Step 2: 构建验证**

Run: `cd backend && dotnet build OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Api/Controllers/ProcessesController.cs
git commit -m "feat(process): API 控制器"
```

---

### Task 9: DbContext + Program.cs 共享改动

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`（DbSet + Seed 权限 + Seed 目标类型 + developerPerms）
- Modify: `backend/src/OneCup.Api/Program.cs`（AddScoped + 4 AddPolicy）

**Interfaces:**
- Consumes: `Process`（Task 1）、`SeedData.PermProcess*` / `SeedData.TargetTypeProcess`（Task 1）、`IProcessService`/`ProcessService`（Task 4/5）。

- [ ] **Step 1: DbContext 加 DbSet**

在 `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs` 的 `// ===== Unit 模块 =====` + `MeasurementUnits` DbSet 行**之后**追加：

```csharp
    // ===== Process 模块（feat/process-mgmt）=====
    public DbSet<Process> Processes => Set<Process>();
```

- [ ] **Step 2: Seed() 权限加 4 条**

在 `Seed()` 方法的 `modelBuilder.Entity<Permission>().HasData(...)` 调用里，在最后一个系统权限 `PermSystemAuditRead` 那行的**末尾分号前**（即把它从最后一个变成倒数），追加 4 条 process 权限。即把：

```csharp
            new Permission { Id = SeedData.PermSystemAuditRead, Code = "system:audit:read", Name = "查看审计日志", CreatedAt = SeedTimestamp }
        );
```

改为：

```csharp
            new Permission { Id = SeedData.PermSystemAuditRead, Code = "system:audit:read", Name = "查看审计日志", CreatedAt = SeedTimestamp },
            // Process 模块
            new Permission { Id = SeedData.PermProcessRead, Code = "process:read", Name = "查看工序", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermProcessCreate, Code = "process:create", Name = "录入工序", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermProcessUpdate, Code = "process:update", Name = "编辑工序", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermProcessDelete, Code = "process:delete", Name = "删除工序", CreatedAt = SeedTimestamp }
        );
```

- [ ] **Step 3: Seed() developerPerms 加 process:read**

在 `Seed()` 的 `var developerPerms = new[] { ... };` 数组里，把 `SeedData.PermSystemAuditRead` 那行**之后**追加一行（紧接其后，逗号结尾）：

```csharp
            SeedData.PermSystemAuditRead,
            SeedData.PermProcessRead
```

- [ ] **Step 4: Seed() NumberingTargetType 加 process 一条**

在 `Seed()` 的 `modelBuilder.Entity<NumberingTargetType>().HasData(...)` 里，把 `TargetTypeProduct` 行**末尾分号前**（变倒数），追加 process 行：

```csharp
            new NumberingTargetType { Id = SeedData.TargetTypeProduct, Code = "product", NameZh = "产品", NameEn = "Product", SortOrder = 6, IsActive = true, CreatedAt = SeedTimestamp },
            new NumberingTargetType { Id = SeedData.TargetTypeProcess, Code = "process", NameZh = "工序", NameEn = "Process", SortOrder = 7, IsActive = true, CreatedAt = SeedTimestamp }
        );
```

- [ ] **Step 5: Program.cs 加 AddScoped**

在 `backend/src/OneCup.Api/Program.cs` 的 `// ===== Unit 模块 =====` + `AddScoped<IMeasurementUnitService>` 行**之后**追加：

```csharp
// ===== Process 模块 =====
builder.Services.AddScoped<IProcessService, ProcessService>();
```

- [ ] **Step 6: Program.cs 加 4 条 AddPolicy**

在 `AddAuthorization` 块里，`product:delete` 那行**之后**（紧接其后）追加：

```csharp
    options.AddPolicy("process:read", p => p.RequireClaim("perm_codes", "process:read"));
    options.AddPolicy("process:create", p => p.RequireClaim("perm_codes", "process:create"));
    options.AddPolicy("process:update", p => p.RequireClaim("perm_codes", "process:update"));
    options.AddPolicy("process:delete", p => p.RequireClaim("perm_codes", "process:delete"));
```

- [ ] **Step 7: 构建验证**

Run: `cd backend && dotnet build OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 8: 跑全部测试确认无回归**

Run: `cd backend && dotnet test OneCup.sln`
Expected: 全部 PASS（含新增 Process 测试 + 既有测试无回归）

- [ ] **Step 9: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs backend/src/OneCup.Api/Program.cs
git commit -m "feat(process): DbContext 种子(4权限+1目标类型+developer read) + Program 注册"
```

---

### Task 10: EF 迁移 AddProcessModule

**Files:**
- Auto-generated: `backend/src/OneCup.Infrastructure/Migrations/<ts>_AddProcessModule.cs`(+`.Designer.cs`) + `OneCupDbContextModelSnapshot.cs`（自动刷新）

- [ ] **Step 1: 生成迁移**

Run: `cd backend && dotnet ef migrations add AddProcessModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api`
Expected: 生成 `<时间戳>_AddProcessModule.cs` + `.Designer.cs`，并刷新 `OneCupDbContextModelSnapshot.cs`。

- [ ] **Step 2: 人工核对迁移内容**

打开生成的 `<ts>_AddProcessModule.cs`，核对 `Up()` 方法包含：
- `migrationBuilder.CreateTable("processes", ...)` 含全部列（id/code/name/category/sort_order/remark/is_active/is_deleted/created_at/updated_at）
- `migrationBuilder.CreateUniqueConstraint` 或 `CreateIndex` 覆盖 `code` 唯一 + `(name, category)` 复合唯一
- `migrationBuilder.InsertData` 4 条 Permission（process:read/create/update/delete）+ 1 条 NumberingTargetType（process）+ developerPerms 关联行（如有）
Expected: 上述内容齐全；若缺失说明 Task 9 的 Seed 改动未生效，回查。

- [ ] **Step 3: 构建验证**

Run: `cd backend && dotnet build OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Migrations/
git commit -m "feat(process): EF 迁移 AddProcessModule"
```

> 注：`ModelSnapshot.cs` 与其他分支必冲突，留给合并阶段方案 B（合同 §6.4），本分支只生成自己的快照版本。

---

### Task 11: 前端 API 客户端

**Files:**
- Create: `frontend/src/api/process.ts`

- [ ] **Step 1: 创建 process.ts**

创建 `frontend/src/api/process.ts`（照 customer.ts；双泛型 `<unknown, T>` 约定）：

```typescript
import request from './request';

// ── 类型 ──
export interface ProcessListItem {
  id: string;
  code: string;
  name: string;
  category?: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
}

export interface ProcessDetail extends ProcessListItem {
  remark?: string;
  updatedAt?: string;
}

export interface ProcessPagedResult {
  items: ProcessListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ProcessQuery {
  keyword?: string;
  category?: string;
  isActive?: boolean;
  page: number;
  pageSize: number;
}

export interface ProcessFormData {
  name: string;
  category?: string;
  sortOrder: number;
  remark?: string;
  isActive: boolean;
}

// ── API ──
// 注：request.ts 响应拦截器返回 response.data，故使用双泛型 <unknown, T>
// 使 Promise 解析为 T（与 customer.ts / numbering.ts 一致）。
export function getProcesses(params: ProcessQuery) {
  return request.get<unknown, ProcessPagedResult>('/api/processes', { params });
}

export function getProcess(id: string) {
  return request.get<unknown, ProcessDetail>(`/api/processes/${id}`);
}

export function createProcess(data: ProcessFormData) {
  return request.post<unknown, ProcessDetail>('/api/processes', data);
}

export function updateProcess(id: string, data: ProcessFormData) {
  return request.put<unknown, ProcessDetail>(`/api/processes/${id}`, data);
}

export function deleteProcess(id: string) {
  return request.delete(`/api/processes/${id}`);
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/api/process.ts
git commit -m "feat(process): 前端 API 客户端"
```

---

### Task 12: 前端 locale + style

**Files:**
- Create: `frontend/src/pages/business/process/locale/index.ts`
- Create: `frontend/src/pages/business/process/locale/zh-CN.ts`
- Create: `frontend/src/pages/business/process/locale/en-US.ts`
- Create: `frontend/src/pages/business/process/style/index.module.less`

- [ ] **Step 1: 创建 locale/index.ts**

```typescript
import zhCN from './zh-CN';
import enUS from './en-US';

export default { 'zh-CN': zhCN, 'en-US': enUS };
```

- [ ] **Step 2: 创建 locale/zh-CN.ts**

```typescript
export default {
  'process.title': '工序管理',
  'process.search.name': '工序名称',
  'process.search.category': '工序分类',
  'process.search.status': '启用状态',
  'process.column.code': '工序编号',
  'process.column.name': '工序名称',
  'process.column.category': '分类',
  'process.column.sortOrder': '排序',
  'process.column.status': '状态',
  'process.column.createdAt': '创建时间',
  'process.column.operations': '操作',
  'process.active': '启用',
  'process.inactive': '停用',
  'process.button.create': '新建工序',
  'process.button.search': '查询',
  'process.button.reset': '重置',
  'process.button.view': '查看',
  'process.button.edit': '编辑',
  'process.button.delete': '删除',
  'process.form.title.create': '新建工序',
  'process.form.title.edit': '编辑工序',
  'process.form.code': '工序编号',
  'process.form.code.placeholder': '创建时自动生成',
  'process.form.code.previewing': '编号预览中…',
  'process.form.noRule.block': '检测到尚未为工序配置编号规则，无法新建工序。请先到「编号管理」为工序配置一条启用的编号规则，再回来新建。',
  'process.form.name': '工序名称',
  'process.form.category': '工序分类',
  'process.form.sortOrder': '排序号',
  'process.form.remark': '备注',
  'process.form.isActive': '启用状态',
  'process.detail.title': '工序详情',
  'process.message.deleteOk': '确定删除该工序吗？',
  'process.message.deleteSuccess': '删除成功',
  'process.message.createSuccess': '创建成功',
  'process.message.updateSuccess': '更新成功',
  'process.message.loading': '加载中…',
  'process.message.loadFailed': '加载失败',
  'process.error.noNumberingRule': '请先在编号管理为工序配置启用规则',
};
```

- [ ] **Step 3: 创建 locale/en-US.ts**

```typescript
export default {
  'process.title': 'Process',
  'process.search.name': 'Name',
  'process.search.category': 'Category',
  'process.search.status': 'Status',
  'process.column.code': 'Code',
  'process.column.name': 'Name',
  'process.column.category': 'Category',
  'process.column.sortOrder': 'Sort',
  'process.column.status': 'Status',
  'process.column.createdAt': 'Created At',
  'process.column.operations': 'Operations',
  'process.active': 'Active',
  'process.inactive': 'Inactive',
  'process.button.create': 'New Process',
  'process.button.search': 'Search',
  'process.button.reset': 'Reset',
  'process.button.view': 'View',
  'process.button.edit': 'Edit',
  'process.button.delete': 'Delete',
  'process.form.title.create': 'New Process',
  'process.form.title.edit': 'Edit Process',
  'process.form.code': 'Process Code',
  'process.form.code.placeholder': 'Auto-generated on create',
  'process.form.code.previewing': 'Previewing code…',
  'process.form.noRule.block': 'No active numbering rule configured for process. Please configure one in "Numbering" first, then return here to create a process.',
  'process.form.name': 'Name',
  'process.form.category': 'Category',
  'process.form.sortOrder': 'Sort Order',
  'process.form.remark': 'Remark',
  'process.form.isActive': 'Active',
  'process.detail.title': 'Process Detail',
  'process.message.deleteOk': 'Delete this process?',
  'process.message.deleteSuccess': 'Deleted',
  'process.message.createSuccess': 'Created',
  'process.message.updateSuccess': 'Updated',
  'process.message.loading': 'Loading…',
  'process.message.loadFailed': 'Load failed',
  'process.error.noNumberingRule': 'Please configure an active numbering rule for process first',
};
```

- [ ] **Step 4: 创建 style/index.module.less**

创建 `frontend/src/pages/business/process/style/index.module.less`（从 `.less.template` 原样复制三段样式）：

```less
.search-form-wrapper {
  display: flex;
  border-bottom: 1px solid var(--color-border-1);
  margin-bottom: 20px;

  .right-button {
    display: flex;
    flex-direction: column;
    justify-content: space-between;
    padding-left: 20px;
    margin-bottom: 20px;
    border-left: 1px solid var(--color-border-2);
    box-sizing: border-box;
  }
}

.search-form {
  padding-right: 20px;
}

.button-group {
  display: flex;
  justify-content: space-between;
  margin-bottom: 20px;
}
```

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/business/process/locale/ frontend/src/pages/business/process/style/
git commit -m "feat(process): 前端 locale + 样式"
```

---

### Task 13: 前端表单（form.tsx）— Convention c02

**Files:**
- Create: `frontend/src/pages/business/process/form.tsx`

- [ ] **Step 1: 创建 form.tsx**

创建 `frontend/src/pages/business/process/form.tsx`（照 customer/form.tsx；遵守 Convention **c02**：previewCode → 只读回填 → null 则禁用表单 + Alert）：

```tsx
import { useEffect, useState } from 'react';
import {
  Alert,
  Form,
  Input,
  InputNumber,
  Message,
  Modal,
  Switch,
} from '@arco-design/web-react';
import {
  ProcessDetail,
  ProcessFormData,
  createProcess,
  updateProcess,
} from '@/api/process';
import { previewCode } from '@/api/numbering';
import useLocale from '@/utils/useLocale';
import locale from './locale';

const FormItem = Form.Item;
const TextArea = Input.TextArea;

export default function ProcessFormModal({
  visible,
  editing,
  onClose,
  onSuccess,
}: {
  visible: boolean;
  editing: ProcessDetail | null; // null = 新建模式
  onClose: () => void;
  onSuccess: () => void;
}) {
  const t = useLocale(locale);
  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  // 新建模式：预览下一个工序编号（不消耗计数）。null = 无启用规则 / 预览中。
  const [previewedCode, setPreviewedCode] = useState<string | null>(null);
  const [codeLoading, setCodeLoading] = useState(false);
  // 无编号规则：阻塞新建（用户填了也提交不了）
  const [noRule, setNoRule] = useState(false);

  useEffect(() => {
    if (visible) {
      setErrorMsg('');
      setNoRule(false);
      if (editing) {
        // 编辑模式：展示实际编号
        setPreviewedCode(editing.code);
        form.setFieldsValue({
          name: editing.name,
          category: editing.category,
          sortOrder: editing.sortOrder,
          remark: editing.remark,
          isActive: editing.isActive,
        });
      } else {
        // 新建模式：预览下一个编号（只读，不消耗计数）
        setPreviewedCode(null);
        setCodeLoading(true);
        previewCode('process')
          .then((res) => {
            // null 表示无启用规则 → 阻塞新建
            if (res.code) {
              setPreviewedCode(res.code);
              setNoRule(false);
            } else {
              setNoRule(true);
            }
          })
          .catch(() => setNoRule(true))
          .finally(() => setCodeLoading(false));
        form.resetFields();
        form.setFieldValue('isActive', true);
        form.setFieldValue('sortOrder', 0);
      }
    }
  }, [visible, editing, form]);

  const handleOk = async () => {
    try {
      const values = (await form.validate()) as ProcessFormData;
      setConfirmLoading(true);
      setErrorMsg('');
      if (editing) {
        await updateProcess(editing.id, values);
        Message.success(t['process.message.updateSuccess']);
      } else {
        await createProcess(values);
        Message.success(t['process.message.createSuccess']);
      }
      onSuccess();
      onClose();
    } catch (err: any) {
      // 后端 400：名称重复 / 无编号规则，展示在顶部 Alert
      const msg = err?.response?.data?.message || err?.message || '';
      if (msg.includes('编号') || msg.includes('rule') || msg.includes('numbering')) {
        setErrorMsg(t['process.error.noNumberingRule']);
      } else {
        setErrorMsg(msg);
      }
    } finally {
      setConfirmLoading(false);
    }
  };

  return (
    <Modal
      title={editing ? t['process.form.title.edit'] : t['process.form.title.create']}
      visible={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={confirmLoading}
      okButtonProps={{ disabled: noRule }}
      unmountOnExit
    >
      {noRule && (
        <Alert type="warning" content={t['process.form.noRule.block']} style={{ marginBottom: 16 }} />
      )}
      {errorMsg && <Alert type="error" content={errorMsg} style={{ marginBottom: 16 }} />}
      <Form form={form} layout="vertical" disabled={noRule}>
        <FormItem label={t['process.form.code']}>
          <Input
            value={previewedCode ?? undefined}
            readOnly
            placeholder={codeLoading ? t['process.form.code.previewing'] : t['process.form.code.placeholder']}
          />
        </FormItem>
        <FormItem label={t['process.form.name']} field="name" rules={[{ required: true }]}>
          <Input maxLength={50} />
        </FormItem>
        <FormItem label={t['process.form.category']} field="category">
          <Input maxLength={50} />
        </FormItem>
        <FormItem label={t['process.form.sortOrder']} field="sortOrder">
          <InputNumber min={0} style={{ width: '100%' }} />
        </FormItem>
        <FormItem label={t['process.form.remark']} field="remark">
          <TextArea maxLength={500} />
        </FormItem>
        <FormItem label={t['process.form.isActive']} field="isActive" triggerPropName="checked">
          <Switch />
        </FormItem>
      </Form>
    </Modal>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/pages/business/process/form.tsx
git commit -m "feat(process): 新建/编辑表单(c02 previewCode 流程)"
```

---

### Task 14: 前端详情（detail.tsx）

**Files:**
- Create: `frontend/src/pages/business/process/detail.tsx`

- [ ] **Step 1: 创建 detail.tsx**

创建 `frontend/src/pages/business/process/detail.tsx`（照 customer/detail.tsx）：

```tsx
import { Descriptions, Drawer } from '@arco-design/web-react';
import { ProcessDetail } from '@/api/process';
import useLocale from '@/utils/useLocale';
import locale from './locale';

export default function ProcessDetailDrawer({
  visible,
  data,
  onClose,
}: {
  visible: boolean;
  data: ProcessDetail | null;
  onClose: () => void;
}) {
  const t = useLocale(locale);
  return (
    <Drawer
      title={t['process.detail.title']}
      visible={visible}
      onCancel={onClose}
      footer={null}
      width={480}
    >
      {data && (
        <Descriptions
          column={1}
          data={[
            { label: t['process.column.code'], value: data.code },
            { label: t['process.column.name'], value: data.name },
            { label: t['process.column.category'], value: data.category || '-' },
            { label: t['process.column.sortOrder'], value: data.sortOrder },
            { label: t['process.form.remark'], value: data.remark || '-' },
            {
              label: t['process.column.status'],
              value: data.isActive ? t['process.active'] : t['process.inactive'],
            },
            { label: t['process.column.createdAt'], value: data.createdAt },
          ]}
        />
      )}
    </Drawer>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/pages/business/process/detail.tsx
git commit -m "feat(process): 详情 Drawer"
```

---

### Task 15: 前端列表页（index.tsx）— 列表查询页标准 + c01

**Files:**
- Create: `frontend/src/pages/business/process/index.tsx`

- [ ] **Step 1: 创建 index.tsx**

创建 `frontend/src/pages/business/process/index.tsx`（从 query-table-page 模板骨架 + customer/index.tsx 结构；遵守「列表查询页标准」+ Convention **c01**：行内删除用 Popconfirm）：

```tsx
import { useEffect, useMemo, useState } from 'react';
import {
  Badge,
  Button,
  Card,
  Form,
  Grid,
  Input,
  Message,
  Popconfirm,
  Select,
  Space,
  Table,
  Typography,
} from '@arco-design/web-react';
import { IconPlus, IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import {
  ProcessDetail,
  ProcessListItem,
  deleteProcess,
  getProcess,
  getProcesses,
} from '@/api/process';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import locale from './locale';
import styles from './style/index.module.less';
import ProcessFormModal from './form';
import ProcessDetailDrawer from './detail';

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
            <FormItem label={t['process.search.name']} field="keyword">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['process.search.category']} field="category">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['process.search.status']} field="isActive">
              <Select allowClear>
                <Option value={true}>{t['process.active']}</Option>
                <Option value={false}>{t['process.inactive']}</Option>
              </Select>
            </FormItem>
          </Col>
        </Row>
      </Form>
      <div className={styles['right-button']}>
        <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>
          {t['process.button.search']}
        </Button>
        <Button icon={<IconRefresh />} onClick={handleReset}>
          {t['process.button.reset']}
        </Button>
      </div>
    </div>
  );
}

export default function ProcessPage() {
  const t = useLocale(locale);
  const [data, setData] = useState<ProcessListItem[]>([]);
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
  const [editing, setEditing] = useState<ProcessDetail | null>(null);
  const [detailVisible, setDetailVisible] = useState(false);
  const [detailData, setDetailData] = useState<ProcessDetail | null>(null);

  function fetchData() {
    setLoading(true);
    getProcesses({
      page: pagination.current,
      pageSize: pagination.pageSize,
      ...formParams,
    })
      .then((res) => {
        setData(res.items || []);
        setPagination((p) => ({ ...p, total: res.total || 0 }));
      })
      .finally(() => setLoading(false));
  }

  function openCreate() {
    setEditing(null);
    setFormVisible(true);
  }
  function openEdit(record: ProcessListItem) {
    // 加载完整 ProcessDetail（含 remark），避免编辑保存后清空备注
    const closeLoading = Message.loading({ content: t['process.message.loading'] });
    getProcess(record.id)
      .then((detail) => {
        setEditing(detail);
        setFormVisible(true);
      })
      .catch(() => Message.error(t['process.message.loadFailed']))
      .finally(() => closeLoading());
  }
  function openDetail(record: ProcessListItem) {
    const closeLoading = Message.loading({ content: t['process.message.loading'] });
    getProcess(record.id)
      .then((detail) => {
        setDetailData(detail);
        setDetailVisible(true);
      })
      .catch(() => Message.error(t['process.message.loadFailed']))
      .finally(() => closeLoading());
  }
  async function handleDelete(record: ProcessListItem) {
    try {
      await deleteProcess(record.id);
      Message.success(t['process.message.deleteSuccess']);
      fetchData();
    } catch {
      // ignore
    }
  }

  const columns = useMemo(
    () => [
      { title: t['process.column.code'], dataIndex: 'code' },
      { title: t['process.column.name'], dataIndex: 'name' },
      { title: t['process.column.category'], dataIndex: 'category', render: (v: string) => v || '-' },
      { title: t['process.column.sortOrder'], dataIndex: 'sortOrder' },
      {
        title: t['process.column.status'],
        dataIndex: 'isActive',
        render: (v: boolean) => (
          <Badge
            status={v ? 'success' : 'default'}
            text={v ? t['process.active'] : t['process.inactive']}
          />
        ),
      },
      { title: t['process.column.createdAt'], dataIndex: 'createdAt' },
      {
        title: t['process.column.operations'],
        dataIndex: 'operations',
        render: (_: any, record: ProcessListItem) => (
          <Space>
            <Button type="text" size="small" onClick={() => openDetail(record)}>
              {t['process.button.view']}
            </Button>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'process', actions: ['update'] }]}
            >
              <Button type="text" size="small" onClick={() => openEdit(record)}>
                {t['process.button.edit']}
              </Button>
            </PermissionWrapper>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'process', actions: ['delete'] }]}
            >
              <Popconfirm
                title={t['process.message.deleteOk']}
                onOk={() => handleDelete(record)}
              >
                <Button type="text" size="small" status="danger">
                  {t['process.button.delete']}
                </Button>
              </Popconfirm>
            </PermissionWrapper>
          </Space>
        ),
      },
    ],
    [t],
  );

  function handleSearch(params: Record<string, any>) {
    setPagination((p) => ({ ...p, current: 1 }));
    setFormParams(params);
  }

  function onChangeTable({ current, pageSize }: any) {
    setPagination((p) => ({ ...p, current, pageSize }));
  }

  useEffect(() => {
    fetchData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pagination.current, pagination.pageSize, JSON.stringify(formParams)]);

  return (
    <Card>
      <Title heading={6}>{t['process.title']}</Title>
      <SearchForm onSearch={handleSearch} />
      <div className={styles['button-group']}>
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'process', actions: ['create'] }]}
          >
            <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
              {t['process.button.create']}
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
      <ProcessFormModal
        visible={formVisible}
        editing={editing}
        onClose={() => setFormVisible(false)}
        onSuccess={fetchData}
      />
      <ProcessDetailDrawer
        visible={detailVisible}
        data={detailData}
        onClose={() => setDetailVisible(false)}
      />
    </Card>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/pages/business/process/index.tsx
git commit -m "feat(process): 列表页(查询页标准+c01 Popconfirm)"
```

---

### Task 16: 前端共享文件（routes/router/locale）

**Files:**
- Modify: `frontend/src/routes.ts`
- Modify: `frontend/src/router.tsx`
- Modify: `frontend/src/locale/index.ts`

- [ ] **Step 1: routes.ts 加菜单项**

在 `frontend/src/routes.ts` 的 `menu.business.children` 数组里，`customer` 项**之后**追加 process 项：

```typescript
      {
        name: 'menu.business.process',
        key: 'business/process',
        requiredPermissions: [
          { resource: 'process', actions: ['read'] },
        ],
      },
```

- [ ] **Step 2: router.tsx 加 lazy import + 路由**

在 `frontend/src/router.tsx`：
- 顶部 lazy import 区（`CustomerPage` 行**之后**）加：
```typescript
const ProcessPage = lazy(() => import('@/pages/business/process'));
```
- children 路由数组里（`business/customer` 块**之后**）加：
```typescript
      {
        path: 'business/process',
        element: withSuspense(
          <RequirePermission resource="process" actions={['read']}>
            <ProcessPage />
          </RequirePermission>
        ),
      },
```

- [ ] **Step 3: locale/index.ts 加菜单文案**

在 `frontend/src/locale/index.ts` 的 en-US 对象里（`'menu.business.customer'` 行**之后**）加：
```typescript
    'menu.business.process': 'Process',
```
在 zh-CN 对象里（`'menu.business.customer'` 行**之后**）加：
```typescript
    'menu.business.process': '工序管理',
```

- [ ] **Step 4: 前端构建验证**

Run: `cd frontend && npm run build`
Expected: BUILD 成功（tsc 编译通过，无类型错误；lazy import 路径正确）。

- [ ] **Step 5: Commit**

```bash
git add frontend/src/routes.ts frontend/src/router.tsx frontend/src/locale/index.ts
git commit -m "feat(process): 菜单/路由/全局文案(business 域追加)"
```

---

## Self-Review

**1. Spec coverage（对照 spec 各节）：**
- §3 实体设计 → Task 1 ✓
- §4 查询规范 → Task 3 ✓（含 ProcessByNameSpec NULL 兜底）
- §5 服务层 → Task 2(Validators) + Task 4(接口) + Task 5(实现) ✓
- §6 API → Task 8 ✓
- §7 种子/权限 → Task 1(Guid 常量) + Task 9(DbContext+Program) ✓；迁移 Task 10 ✓
- §8 前端 → Task 11(api) + Task 12(locale/style) + Task 13(form/c02) + Task 14(detail) + Task 15(index/标准+c01) + Task 16(共享) ✓
- §9 测试 → Task 6(服务) + Task 7(Validator) ✓
- §10 验证清单 → 各 Task 的构建/测试 step + Task 16 前端 build ✓
- §11 红线 → Global Constraints 全覆盖 ✓

**2. Placeholder scan：** 无 TBD/TODO；所有 step 含完整代码。✓

**3. Type consistency：**
- DTO 名一致：`ProcessListItemDto`/`ProcessDto`/`CreateProcessRequest`/`UpdateProcessRequest`（Task 2 定义，Task 5/6/8 引用一致）✓
- Spec 名一致：`ProcessFilterSpec`/`ProcessPagedSpec`/`ProcessByIdSpec`/`ProcessByNameSpec`（Task 3 定义，Task 5 引用一致）✓
- `ProcessByNameSpec` 签名 `(string name, string? category, Guid? excludingId = null)` 在 Task 3 定义，Task 5 的 Create（不传 excludingId）/ Update（传 id）调用一致 ✓
- 前端类型 `ProcessListItem`/`ProcessDetail`/`ProcessFormData`（Task 11 定义，Task 13/14/15 引用一致）✓
- 种子 Guid 值在 Task 1 定义、Task 9 引用一致（32b-32e/0207）✓

无问题。
